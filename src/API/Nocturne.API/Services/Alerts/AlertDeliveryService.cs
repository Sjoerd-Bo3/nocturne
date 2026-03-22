using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// Creates delivery records for an alert instance step and dispatches them
/// to the appropriate channel providers (web_push, webhook, etc.).
/// </summary>
internal sealed class AlertDeliveryService(
    IDbContextFactory<NocturneDbContext> contextFactory,
    ITenantAccessor tenantAccessor,
    ISignalRBroadcastService broadcastService,
    IServiceProvider serviceProvider,
    ILogger<AlertDeliveryService> logger)
    : IAlertDeliveryService
{
    public async Task DispatchAsync(Guid alertInstanceId, int stepOrder, AlertPayload payload, CancellationToken ct)
    {
        var tenantId = tenantAccessor.TenantId;
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        // Find the escalation step for this instance
        var instance = await db.AlertInstances
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == alertInstanceId, ct);

        if (instance is null)
        {
            logger.LogWarning("Alert instance {InstanceId} not found for dispatch", alertInstanceId);
            return;
        }

        // --- Quiet hours check ---
        if (await IsQuietHoursActiveAsync(db, tenantId, instance.AlertExcursionId, ct))
        {
            logger.LogInformation("Alert dispatch suppressed by quiet hours for tenant {TenantId}", tenantId);
            return;
        }

        var step = await db.AlertEscalationSteps
            .AsNoTracking()
            .Include(s => s.Channels)
            .Where(s => s.AlertScheduleId == instance.AlertScheduleId && s.StepOrder == stepOrder)
            .FirstOrDefaultAsync(ct);

        if (step is null)
        {
            logger.LogWarning("No escalation step found for instance {InstanceId}, step {StepOrder}",
                alertInstanceId, stepOrder);
            return;
        }

        var payloadJson = JsonSerializer.Serialize(payload);

        // Create delivery records for each channel
        foreach (var channel in step.Channels)
        {
            var delivery = new AlertDeliveryEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                AlertInstanceId = alertInstanceId,
                EscalationStepId = step.Id,
                ChannelType = channel.ChannelType,
                Destination = channel.Destination,
                Payload = payloadJson,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
            };

            db.AlertDeliveries.Add(delivery);
        }

        await db.SaveChangesAsync(ct);

        // Broadcast alert_dispatch for the fast path (web clients)
        try
        {
            await broadcastService.BroadcastAlertEventAsync("alert_dispatch", payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast alert_dispatch for instance {InstanceId}", alertInstanceId);
        }

        // Dispatch to channel providers
        var deliveries = await db.AlertDeliveries
            .Where(d => d.AlertInstanceId == alertInstanceId && d.EscalationStepId == step.Id && d.Status == "pending")
            .ToListAsync(ct);

        foreach (var delivery in deliveries)
        {
            try
            {
                await DispatchToProviderAsync(delivery, payload, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispatch delivery {DeliveryId} via {ChannelType}",
                    delivery.Id, delivery.ChannelType);
                await MarkFailedAsync(delivery.Id, ex.Message, ct);
            }
        }
    }

    public async Task MarkDeliveredAsync(Guid deliveryId, string? platformMessageId, string? platformThreadId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var delivery = await db.AlertDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return;

        delivery.Status = "delivered";
        delivery.DeliveredAt = DateTime.UtcNow;
        delivery.PlatformMessageId = platformMessageId;
        delivery.PlatformThreadId = platformThreadId;
        await db.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid deliveryId, string error, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var delivery = await db.AlertDeliveries.FirstOrDefaultAsync(d => d.Id == deliveryId, ct);
        if (delivery is null) return;

        delivery.Status = "failed";
        delivery.RetryCount++;
        delivery.LastError = error;
        await db.SaveChangesAsync(ct);
    }

    private async Task DispatchToProviderAsync(AlertDeliveryEntity delivery, AlertPayload payload, CancellationToken ct)
    {
        switch (delivery.ChannelType)
        {
            case "web_push":
                var webPushProvider = serviceProvider.GetService<Providers.WebPushProvider>();
                if (webPushProvider is not null)
                {
                    await webPushProvider.SendAsync(payload, ct);
                    await MarkDeliveredAsync(delivery.Id, null, null, ct);
                }
                break;

            case "webhook":
                var webhookProvider = serviceProvider.GetService<Providers.WebhookProvider>();
                if (webhookProvider is not null)
                {
                    await webhookProvider.SendAsync(delivery.Destination, payload, ct);
                    await MarkDeliveredAsync(delivery.Id, null, null, ct);
                }
                break;

            case var ch when Providers.ChatBotProvider.SupportedChannelTypes.Contains(ch):
                var chatBotProvider = serviceProvider.GetService<Providers.ChatBotProvider>();
                if (chatBotProvider is not null)
                {
                    await chatBotProvider.SendAsync(delivery.Id, delivery.ChannelType, delivery.Destination, payload, ct);
                    // Do not mark as delivered here — the bot reports back via MarkDeliveredAsync
                }
                break;

            default:
                logger.LogWarning("Unsupported channel type '{ChannelType}' for delivery {DeliveryId}",
                    delivery.ChannelType, delivery.Id);
                break;
        }
    }

    /// <summary>
    /// Checks whether quiet hours are currently active for the tenant and whether
    /// the alert severity allows bypassing them.
    /// </summary>
    private async Task<bool> IsQuietHoursActiveAsync(NocturneDbContext db, Guid tenantId, Guid excursionId, CancellationToken ct)
    {
        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.QuietHoursStart, t.QuietHoursEnd, t.QuietHoursOverrideCritical, t.Timezone })
            .FirstOrDefaultAsync(ct);

        if (tenant is null) return false;

        // Quiet hours disabled if either bound is null
        if (tenant.QuietHoursStart is null || tenant.QuietHoursEnd is null) return false;

        // Determine current time in tenant's timezone
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(tenant.Timezone);
        }
        catch
        {
            tz = TimeZoneInfo.Utc;
        }

        var tenantNow = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz));
        var start = tenant.QuietHoursStart.Value;
        var end = tenant.QuietHoursEnd.Value;

        bool inQuietWindow;
        if (start <= end)
        {
            // Same-day window, e.g. 13:00-15:00
            inQuietWindow = tenantNow >= start && tenantNow < end;
        }
        else
        {
            // Cross-midnight window, e.g. 22:00-07:00
            inQuietWindow = tenantNow >= start || tenantNow < end;
        }

        if (!inQuietWindow) return false;

        // Check if critical severity bypasses quiet hours
        if (tenant.QuietHoursOverrideCritical)
        {
            // Navigate instance → excursion → rule to get severity
            var severity = await db.AlertExcursions
                .AsNoTracking()
                .Where(e => e.Id == excursionId)
                .Join(db.AlertRules, e => e.AlertRuleId, r => r.Id, (_, r) => r.Severity)
                .FirstOrDefaultAsync(ct);

            if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
                return false; // Critical bypasses quiet hours
        }

        return true;
    }
}

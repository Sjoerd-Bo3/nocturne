using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// Acknowledges ALL active excursions for a tenant. Acknowledgement halts
/// escalation but does not close the excursion — hysteresis still runs.
/// </summary>
internal sealed class AlertAcknowledgementService(
    IDbContextFactory<NocturneDbContext> contextFactory,
    ISignalRBroadcastService broadcastService,
    ILogger<AlertAcknowledgementService> logger)
    : IAlertAcknowledgementService
{
    public async Task AcknowledgeAllAsync(Guid tenantId, string acknowledgedBy, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var now = DateTime.UtcNow;

        // 1. Load all active excursions for the tenant (EndedAt IS NULL)
        var excursions = await db.AlertExcursions
            .Where(e => e.TenantId == tenantId && e.EndedAt == null)
            .ToListAsync(ct);

        if (excursions.Count == 0)
        {
            logger.LogDebug("No active excursions to acknowledge for tenant {TenantId}", tenantId);
            return;
        }

        // 2. Acknowledge each excursion
        foreach (var excursion in excursions)
        {
            excursion.AcknowledgedAt = now;
            excursion.AcknowledgedBy = acknowledgedBy;
        }

        // 3. Load corresponding alert instances and set to "acknowledged"
        var excursionIds = excursions.Select(e => e.Id).ToList();
        var instances = await db.AlertInstances
            .Where(i => excursionIds.Contains(i.AlertExcursionId) && i.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var instance in instances)
        {
            instance.Status = "acknowledged";
            instance.NextEscalationAt = null;
        }

        await db.SaveChangesAsync(ct);

        // 5. Broadcast alert_acknowledged event via SignalR
        try
        {
            await broadcastService.BroadcastAlertEventAsync("alert_acknowledged", new
            {
                tenantId,
                acknowledgedBy,
                acknowledgedAt = now,
                excursionCount = excursions.Count,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast alert_acknowledged for tenant {TenantId}", tenantId);
        }

        logger.LogInformation(
            "Acknowledged {ExcursionCount} excursions and {InstanceCount} instances for tenant {TenantId} by {AcknowledgedBy}",
            excursions.Count, instances.Count, tenantId, acknowledgedBy);
    }
}

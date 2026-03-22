using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// Background service that runs every 30 seconds to:
/// 1. Advance escalations whose delay has elapsed.
/// 2. Close excursions whose hysteresis window has expired.
/// 3. Evaluate signal loss rules for tenants with stale readings.
/// 4. Check snoozed instances for smart snooze extension or re-fire.
/// </summary>
public class AlertSweepService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AlertSweepService> _logger;

    public AlertSweepService(
        IServiceProvider serviceProvider,
        ILogger<AlertSweepService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Alert Sweep Service started");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await AdvanceEscalationsAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing escalations");
            }

            try
            {
                await CloseHysteresisWindowsAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing hysteresis windows");
            }

            try
            {
                await EvaluateSignalLossAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating signal loss");
            }

            try
            {
                await CheckSnoozedInstancesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking snoozed instances");
            }
        }

        _logger.LogInformation("Alert Sweep Service stopped");
    }

    /// <summary>
    /// Query instances with status "escalating" whose NextEscalationAt has passed.
    /// Advance each to the next step.
    /// </summary>
    private async Task AdvanceEscalationsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;

        var instances = await db.AlertInstances
            .Where(i => i.Status == "escalating"
                        && i.NextEscalationAt != null
                        && i.NextEscalationAt <= now
                        && i.SnoozedUntil == null)
            .ToListAsync(ct);

        if (instances.Count == 0) return;

        _logger.LogDebug("Advancing {Count} escalations", instances.Count);

        var deliveryService = scope.ServiceProvider.GetRequiredService<IAlertDeliveryService>();

        foreach (var instance in instances)
        {
            try
            {
                await AlertOrchestrator.AdvanceEscalationAsync(
                    instance, instance.TenantId, db, now, ct, deliveryService, _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error advancing escalation for instance {InstanceId}", instance.Id);
            }
        }
    }

    /// <summary>
    /// Close excursions that are in hysteresis and whose window has expired.
    /// </summary>
    private async Task CloseHysteresisWindowsAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;

        // Load open excursions that have hysteresis started
        var excursions = await db.AlertExcursions
            .Where(e => e.EndedAt == null && e.HysteresisStartedAt != null)
            .ToListAsync(ct);

        if (excursions.Count == 0) return;

        // Join to rules to get hysteresis minutes
        var ruleIds = excursions.Select(e => e.AlertRuleId).Distinct().ToList();
        var rules = await db.AlertRules
            .AsNoTracking()
            .Where(r => ruleIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, ct);

        var closedCount = 0;

        foreach (var excursion in excursions)
        {
            if (!rules.TryGetValue(excursion.AlertRuleId, out var rule)) continue;

            var expiry = excursion.HysteresisStartedAt!.Value.AddMinutes(rule.HysteresisMinutes);
            if (now < expiry) continue;

            // Close the excursion
            excursion.EndedAt = now;
            excursion.HysteresisStartedAt = null;

            // Reset tracker state
            var trackerState = await db.AlertTrackerState
                .FirstOrDefaultAsync(s => s.AlertRuleId == excursion.AlertRuleId, ct);

            if (trackerState is not null)
            {
                trackerState.State = "idle";
                trackerState.ConfirmationCount = 0;
                trackerState.ActiveExcursionId = null;
                trackerState.UpdatedAt = now;
            }

            // Resolve any open instances for this excursion
            var instances = await db.AlertInstances
                .Where(i => i.AlertExcursionId == excursion.Id && i.ResolvedAt == null)
                .ToListAsync(ct);

            foreach (var instance in instances)
            {
                instance.Status = "resolved";
                instance.ResolvedAt = now;
                instance.NextEscalationAt = null;
            }

            closedCount++;
        }

        if (closedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Closed {Count} hysteresis-expired excursions", closedCount);
        }
    }

    /// <summary>
    /// Evaluate signal loss rules: for tenants whose last reading is older than the timeout,
    /// feed conditionMet=true into the excursion tracker.
    /// </summary>
    private async Task EvaluateSignalLossAsync(CancellationToken ct)
    {
        using var lookupScope = _serviceProvider.CreateScope();
        var factory = lookupScope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var lookupDb = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;

        // Find signal_loss rules joined with tenants where last_reading_at is stale
        var signalLossRules = await lookupDb.AlertRules
            .AsNoTracking()
            .Where(r => r.IsEnabled && r.ConditionType == "signal_loss")
            .ToListAsync(ct);

        if (signalLossRules.Count == 0) return;

        // Group rules by tenant
        var rulesByTenant = signalLossRules.GroupBy(r => r.TenantId);

        foreach (var tenantGroup in rulesByTenant)
        {
            var tenantId = tenantGroup.Key;

            // Get tenant's last reading time
            var tenant = await lookupDb.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId && t.IsActive)
                .Select(t => new { t.Id, t.Slug, t.DisplayName, t.LastReadingAt })
                .FirstOrDefaultAsync(ct);

            if (tenant is null) continue;

            foreach (var rule in tenantGroup)
            {
                try
                {
                    // Parse timeout from condition params
                    var conditionParams = System.Text.Json.JsonSerializer.Deserialize<SignalLossCondition>(rule.ConditionParams);
                    if (conditionParams is null) continue;

                    var timeout = TimeSpan.FromMinutes(conditionParams.TimeoutMinutes);
                    var lastReading = tenant.LastReadingAt ?? DateTime.MinValue;

                    if (now - lastReading < timeout) continue;

                    // Signal loss detected for this rule. Create a scoped service and evaluate.
                    using var tenantScope = _serviceProvider.CreateScope();
                    var tenantAccessor = tenantScope.ServiceProvider.GetRequiredService<ITenantAccessor>();
                    tenantAccessor.SetTenant(new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, true));

                    var excursionTracker = tenantScope.ServiceProvider.GetRequiredService<IExcursionTracker>();
                    await excursionTracker.ProcessEvaluationAsync(rule.Id, true, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error evaluating signal loss for rule {RuleId}", rule.Id);
                }
            }
        }
    }

    /// <summary>
    /// Check snoozed instances whose snooze has expired.
    /// If smart snooze is enabled and the glucose trend is favorable, extend the snooze.
    /// Otherwise, clear the snooze so the alert re-fires and escalation resumes.
    /// </summary>
    private async Task CheckSnoozedInstancesAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var db = await factory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;

        // Find instances with expired snooze that are still active
        var instances = await db.AlertInstances
            .Where(i => i.SnoozedUntil != null
                        && i.SnoozedUntil <= now
                        && i.Status != "resolved"
                        && i.Status != "acknowledged")
            .ToListAsync(ct);

        if (instances.Count == 0) return;

        _logger.LogDebug("Processing {Count} expired snoozed instances", instances.Count);

        // Load the excursions and rules we need
        var excursionIds = instances.Select(i => i.AlertExcursionId).Distinct().ToList();
        var excursionToRule = await db.AlertExcursions
            .AsNoTracking()
            .Where(e => excursionIds.Contains(e.Id))
            .Join(db.AlertRules, e => e.AlertRuleId, r => r.Id, (e, r) => new { ExcursionId = e.Id, Rule = r })
            .ToDictionaryAsync(x => x.ExcursionId, x => x.Rule, ct);

        // Gather distinct tenant IDs so we can batch-load latest glucose
        var tenantIds = instances.Select(i => i.TenantId).Distinct().ToList();
        var latestGlucoseByTenant = new Dictionary<Guid, double?>();

        foreach (var tenantId in tenantIds)
        {
            var latestReading = await db.SensorGlucose
                .AsNoTracking()
                .Where(sg => sg.TenantId == tenantId)
                .OrderByDescending(sg => sg.Timestamp)
                .Select(sg => sg.TrendRate)
                .FirstOrDefaultAsync(ct);

            latestGlucoseByTenant[tenantId] = latestReading;
        }

        var modifiedCount = 0;

        foreach (var instance in instances)
        {
            if (!excursionToRule.TryGetValue(instance.AlertExcursionId, out var rule))
            {
                // Can't resolve rule — clear snooze to be safe
                instance.SnoozedUntil = null;
                modifiedCount++;
                continue;
            }

            // Parse client configuration for snooze settings
            var smartSnooze = false;
            var smartSnoozeExtendMinutes = 15;
            var maxCount = 3;

            try
            {
                using var doc = JsonDocument.Parse(rule.ClientConfiguration);
                if (doc.RootElement.TryGetProperty("snooze", out var snoozeEl))
                {
                    if (snoozeEl.TryGetProperty("smartSnooze", out var smartEl))
                        smartSnooze = smartEl.GetBoolean();
                    if (snoozeEl.TryGetProperty("smartSnoozeExtendMinutes", out var extendEl))
                        smartSnoozeExtendMinutes = extendEl.GetInt32();
                    if (snoozeEl.TryGetProperty("maxCount", out var maxEl))
                        maxCount = maxEl.GetInt32();
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse client configuration for rule {RuleId}", rule.Id);
            }

            if (smartSnooze && instance.SnoozeCount < maxCount)
            {
                // Determine if glucose trend is favorable
                var favorable = IsTrendFavorable(rule, latestGlucoseByTenant.GetValueOrDefault(instance.TenantId));

                if (favorable)
                {
                    instance.SnoozedUntil = now.AddMinutes(smartSnoozeExtendMinutes);
                    instance.SnoozeCount++;
                    _logger.LogDebug(
                        "Smart snooze extended instance {InstanceId} by {Minutes}m (count: {Count})",
                        instance.Id, smartSnoozeExtendMinutes, instance.SnoozeCount);
                }
                else
                {
                    instance.SnoozedUntil = null;
                    _logger.LogDebug(
                        "Smart snooze cleared for instance {InstanceId} — trend not favorable",
                        instance.Id);
                }
            }
            else
            {
                // Smart snooze disabled or max count reached — clear snooze
                instance.SnoozedUntil = null;
                _logger.LogDebug(
                    "Snooze expired for instance {InstanceId} (smartSnooze={Smart}, count={Count}/{Max})",
                    instance.Id, smartSnooze, instance.SnoozeCount, maxCount);
            }

            modifiedCount++;
        }

        if (modifiedCount > 0)
        {
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Processed {Count} expired snoozed instances", modifiedCount);
        }
    }

    /// <summary>
    /// Determines whether the current glucose trend is favorable for extending a snooze.
    /// For "below" (low alerts): favorable if BG is rising (trend rate > 0).
    /// For "above" (high alerts): favorable if BG is falling (trend rate &lt; 0).
    /// For other condition types: not favorable (don't extend).
    /// </summary>
    private static bool IsTrendFavorable(Nocturne.Infrastructure.Data.Entities.AlertRuleEntity rule, double? trendRate)
    {
        if (trendRate is null) return false;
        if (rule.ConditionType != "threshold") return false;

        try
        {
            var condition = JsonSerializer.Deserialize<ThresholdCondition>(rule.ConditionParams);
            if (condition is null) return false;

            return condition.Direction.ToLowerInvariant() switch
            {
                "below" => trendRate > 0,  // Low alert: favorable if BG rising
                "above" => trendRate < 0,  // High alert: favorable if BG falling
                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}

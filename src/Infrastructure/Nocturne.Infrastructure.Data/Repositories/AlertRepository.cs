using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for alert orchestration queries and mutations.
/// Methods are virtual to allow mocking with CallBase in tests.
/// </summary>
public class AlertRepository : IAlertRepository
{
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory;

    public AlertRepository(IDbContextFactory<NocturneDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public virtual async Task<IReadOnlyList<AlertRuleSnapshot>> GetEnabledRulesAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsEnabled)
            .OrderBy(r => r.SortOrder)
            .Select(r => new AlertRuleSnapshot(
                r.Id, r.TenantId, r.Name, r.ConditionType,
                r.ConditionParams, r.HysteresisMinutes, r.ConfirmationReadings,
                r.Severity, r.ClientConfiguration, r.SortOrder))
            .ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<AlertScheduleSnapshot>> GetSchedulesForRuleAsync(
        Guid ruleId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertSchedules
            .AsNoTracking()
            .Where(s => s.AlertRuleId == ruleId)
            .Select(s => new AlertScheduleSnapshot(
                s.Id, s.AlertRuleId, s.Name, s.IsDefault,
                s.DaysOfWeek, s.StartTime, s.EndTime, s.Timezone))
            .ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<AlertEscalationStepSnapshot>> GetEscalationStepsAsync(
        Guid scheduleId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertEscalationSteps
            .AsNoTracking()
            .Where(e => e.AlertScheduleId == scheduleId)
            .OrderBy(e => e.StepOrder)
            .Select(e => new AlertEscalationStepSnapshot(
                e.Id, e.AlertScheduleId, e.StepOrder, e.DelaySeconds))
            .ToListAsync(ct);
    }

    public virtual async Task<AlertInstanceSnapshot> CreateInstanceAsync(
        CreateAlertInstanceRequest request, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = new AlertInstanceEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = request.TenantId,
            AlertExcursionId = request.ExcursionId,
            AlertScheduleId = request.ScheduleId,
            CurrentStepOrder = request.InitialStepOrder,
            Status = request.Status,
            TriggeredAt = request.TriggeredAt,
            NextEscalationAt = request.NextEscalationAt,
        };

        context.AlertInstances.Add(entity);
        await context.SaveChangesAsync(ct);

        return new AlertInstanceSnapshot(
            entity.Id, entity.TenantId, entity.AlertExcursionId, entity.AlertScheduleId,
            entity.CurrentStepOrder, entity.Status, entity.TriggeredAt,
            entity.NextEscalationAt, entity.SnoozedUntil, entity.SnoozeCount);
    }

    public virtual async Task<IReadOnlyList<AlertInstanceSnapshot>> GetEscalatingInstancesDueAsync(
        DateTime asOf, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertInstances
            .AsNoTracking()
            .Where(i => i.Status == "escalating"
                        && i.NextEscalationAt != null
                        && i.NextEscalationAt <= asOf
                        && i.SnoozedUntil == null)
            .Select(i => new AlertInstanceSnapshot(
                i.Id, i.TenantId, i.AlertExcursionId, i.AlertScheduleId,
                i.CurrentStepOrder, i.Status, i.TriggeredAt,
                i.NextEscalationAt, i.SnoozedUntil, i.SnoozeCount))
            .ToListAsync(ct);
    }

    public virtual async Task<IReadOnlyList<AlertInstanceSnapshot>> GetInstancesForExcursionAsync(
        Guid excursionId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertInstances
            .AsNoTracking()
            .Where(i => i.AlertExcursionId == excursionId)
            .Select(i => new AlertInstanceSnapshot(
                i.Id, i.TenantId, i.AlertExcursionId, i.AlertScheduleId,
                i.CurrentStepOrder, i.Status, i.TriggeredAt,
                i.NextEscalationAt, i.SnoozedUntil, i.SnoozeCount))
            .ToListAsync(ct);
    }

    public virtual async Task ResolveInstancesForExcursionAsync(
        Guid excursionId, DateTime resolvedAt, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        await context.AlertInstances
            .Where(i => i.AlertExcursionId == excursionId
                        && i.Status != "resolved")
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, "resolved")
                .SetProperty(i => i.ResolvedAt, resolvedAt), ct);
    }

    public virtual async Task UpdateInstanceAsync(
        UpdateAlertInstanceRequest request, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var entity = await context.AlertInstances
            .FirstOrDefaultAsync(i => i.Id == request.Id, ct);

        if (entity == null) return;

        if (request.CurrentStepOrder.HasValue)
            entity.CurrentStepOrder = request.CurrentStepOrder.Value;

        if (request.Status is not null)
            entity.Status = request.Status;

        if (request.NextEscalationAt.HasValue)
            entity.NextEscalationAt = request.NextEscalationAt == DateTime.MinValue
                ? null : request.NextEscalationAt.Value;

        if (request.SnoozedUntil.HasValue)
            entity.SnoozedUntil = request.SnoozedUntil == DateTime.MinValue
                ? null : request.SnoozedUntil.Value;

        if (request.SnoozeCount.HasValue)
            entity.SnoozeCount = request.SnoozeCount.Value;

        await context.SaveChangesAsync(ct);
    }

    public virtual async Task ExpirePendingDeliveriesAsync(
        IReadOnlyList<Guid> instanceIds, CancellationToken ct)
    {
        if (instanceIds.Count == 0) return;

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        await context.AlertDeliveries
            .Where(d => instanceIds.Contains(d.AlertInstanceId)
                        && d.Status == "pending")
            .ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, "expired"), ct);
    }

    public virtual async Task<int> CountActiveExcursionsAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertExcursions
            .AsNoTracking()
            .Where(e => e.TenantId == tenantId && e.EndedAt == null)
            .CountAsync(ct);
    }

    public virtual async Task<IReadOnlyList<HysteresisExcursionSnapshot>> GetExcursionsInHysteresisAsync(
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertExcursions
            .AsNoTracking()
            .Where(e => e.HysteresisStartedAt != null && e.EndedAt == null)
            .Join(context.AlertRules,
                e => e.AlertRuleId,
                r => r.Id,
                (e, r) => new HysteresisExcursionSnapshot(
                    e.Id, e.AlertRuleId, e.HysteresisStartedAt, r.HysteresisMinutes))
            .ToListAsync(ct);
    }

    public virtual async Task CloseHysteresisExcursionAsync(
        Guid excursionId, Guid alertRuleId, DateTime endedAt, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // Close the excursion
        var excursion = await context.AlertExcursions
            .FirstOrDefaultAsync(e => e.Id == excursionId, ct);

        if (excursion != null)
        {
            excursion.EndedAt = endedAt;
        }

        // Resolve any remaining instances for this excursion
        await context.AlertInstances
            .Where(i => i.AlertExcursionId == excursionId && i.Status != "resolved")
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, "resolved")
                .SetProperty(i => i.ResolvedAt, endedAt), ct);

        // Reset tracker state for the rule to idle
        var tracker = await context.AlertTrackerState
            .FirstOrDefaultAsync(t => t.AlertRuleId == alertRuleId, ct);

        if (tracker != null)
        {
            tracker.State = "idle";
            tracker.ConfirmationCount = 0;
            tracker.ActiveExcursionId = null;
            tracker.UpdatedAt = endedAt;
        }

        await context.SaveChangesAsync(ct);
    }

    public virtual async Task<TenantAlertContext?> GetTenantAlertContextAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new TenantAlertContext(
                t.Id, t.SubjectName ?? string.Empty, t.Slug, t.DisplayName,
                t.IsActive, t.LastReadingAt))
            .FirstOrDefaultAsync(ct);
    }

    public virtual async Task<IReadOnlyList<SignalLossRuleSnapshot>> GetEnabledSignalLossRulesAsync(
        CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertRules
            .AsNoTracking()
            .Where(r => r.IsEnabled && r.ConditionType == "signal_loss")
            .Select(r => new SignalLossRuleSnapshot(r.Id, r.TenantId, r.ConditionParams))
            .ToListAsync(ct);
    }

    public virtual async Task<double?> GetLatestTrendRateAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.SensorGlucose
            .AsNoTracking()
            .Where(sg => sg.TenantId == tenantId)
            .OrderByDescending(sg => sg.Timestamp)
            .Select(sg => sg.TrendRate)
            .FirstOrDefaultAsync(ct);
    }

    public virtual async Task<IReadOnlyList<SnoozedInstanceSnapshot>> GetExpiredSnoozedInstancesAsync(
        DateTime asOf, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        return await context.AlertInstances
            .AsNoTracking()
            .Where(i => i.SnoozedUntil != null
                        && i.SnoozedUntil <= asOf
                        && i.Status != "resolved")
            .Join(context.AlertExcursions,
                i => i.AlertExcursionId,
                e => e.Id,
                (i, e) => new { Instance = i, Excursion = e })
            .Join(context.AlertRules,
                x => x.Excursion.AlertRuleId,
                r => r.Id,
                (x, r) => new SnoozedInstanceSnapshot(
                    x.Instance.Id, x.Instance.TenantId, x.Instance.AlertExcursionId,
                    x.Instance.AlertScheduleId, x.Instance.CurrentStepOrder,
                    x.Instance.Status, x.Instance.SnoozeCount,
                    r.Id, r.ConditionType, r.ConditionParams, r.ClientConfiguration))
            .ToListAsync(ct);
    }

    public virtual async Task SaveChangesAsync(CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        await context.SaveChangesAsync(ct);
    }
}

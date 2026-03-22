using Microsoft.EntityFrameworkCore;
using Nocturne.API.Services.Alerts.Evaluators;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// Wires condition evaluators, excursion tracker, and delivery dispatch together.
/// Called on every new glucose reading to evaluate all enabled alert rules for the tenant.
/// </summary>
internal sealed class AlertOrchestrator(
    ConditionEvaluatorRegistry evaluatorRegistry,
    IExcursionTracker excursionTracker,
    IDbContextFactory<NocturneDbContext> contextFactory,
    ITenantAccessor tenantAccessor,
    IAlertDeliveryService deliveryService,
    ISignalRBroadcastService broadcastService,
    TimeProvider timeProvider,
    ILogger<AlertOrchestrator> logger)
    : IAlertOrchestrator
{
    public async Task EvaluateAsync(SensorContext context, CancellationToken ct)
    {
        var tenantId = tenantAccessor.TenantId;
        if (tenantId == Guid.Empty) return;

        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var rules = await db.AlertRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId && r.IsEnabled)
            .OrderBy(r => r.SortOrder)
            .ToListAsync(ct);

        if (rules.Count == 0) return;

        foreach (var rule in rules)
        {
            try
            {
                await EvaluateRuleAsync(rule, context, tenantId, db, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error evaluating alert rule {AlertRuleId} for tenant {TenantId}",
                    rule.Id, tenantId);
            }
        }
    }

    private async Task EvaluateRuleAsync(
        AlertRuleEntity rule,
        SensorContext context,
        Guid tenantId,
        NocturneDbContext db,
        CancellationToken ct)
    {
        var evaluator = evaluatorRegistry.GetEvaluator(rule.ConditionType);
        if (evaluator is null)
        {
            logger.LogWarning("No evaluator registered for condition type '{ConditionType}'", rule.ConditionType);
            return;
        }

        var conditionMet = evaluator.Evaluate(rule.ConditionParams, context);
        var transition = await excursionTracker.ProcessEvaluationAsync(rule.Id, conditionMet, ct);

        switch (transition.Type)
        {
            case ExcursionTransitionType.ExcursionOpened:
                await HandleExcursionOpened(rule, transition, context, tenantId, db, ct);
                break;

            case ExcursionTransitionType.ExcursionClosed:
                await HandleExcursionClosed(transition, tenantId, db, ct);
                break;

            case ExcursionTransitionType.ExcursionContinues:
                await HandleExcursionContinues(transition, tenantId, db, ct);
                break;
        }
    }

    private async Task HandleExcursionOpened(
        AlertRuleEntity rule,
        ExcursionTransition transition,
        SensorContext context,
        Guid tenantId,
        NocturneDbContext db,
        CancellationToken ct)
    {
        if (!transition.ExcursionId.HasValue) return;

        var excursionId = transition.ExcursionId.Value;

        // Resolve active schedule
        var schedules = await db.AlertSchedules
            .AsNoTracking()
            .Where(s => s.AlertRuleId == rule.Id)
            .ToListAsync(ct);

        if (schedules.Count == 0)
        {
            logger.LogWarning("No schedules found for rule {AlertRuleId}; skipping instance creation", rule.Id);
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var activeSchedule = ScheduleResolver.Resolve(schedules, now);

        // Get escalation steps for step 0
        var steps = await db.AlertEscalationSteps
            .AsNoTracking()
            .Where(s => s.AlertScheduleId == activeSchedule.Id)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(ct);

        // Create alert instance
        var instance = new AlertInstanceEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            AlertExcursionId = excursionId,
            AlertScheduleId = activeSchedule.Id,
            CurrentStepOrder = 0,
            Status = steps.Count > 1 ? "escalating" : "triggered",
            TriggeredAt = now,
            NextEscalationAt = steps.Count > 1 ? now.AddSeconds(steps[0].DelaySeconds) : null,
        };

        db.AlertInstances.Add(instance);
        await db.SaveChangesAsync(ct);

        // Count active excursions for payload
        var activeExcursionCount = await db.AlertExcursions
            .CountAsync(e => e.TenantId == tenantId && e.EndedAt == null, ct);

        // Get tenant subject name
        var tenant = await db.Tenants
            .AsNoTracking()
            .Where(t => t.Id == tenantId)
            .Select(t => new { t.SubjectName, t.DisplayName })
            .FirstOrDefaultAsync(ct);

        var payload = new AlertPayload
        {
            AlertType = rule.ConditionType,
            RuleName = rule.Name,
            GlucoseValue = context.LatestValue,
            Trend = null,
            TrendRate = context.TrendRate,
            ReadingTimestamp = context.LatestTimestamp ?? now,
            ExcursionId = excursionId,
            InstanceId = instance.Id,
            TenantId = tenantId,
            SubjectName = tenant?.SubjectName ?? tenant?.DisplayName ?? "Unknown",
            ActiveExcursionCount = activeExcursionCount,
        };

        // Dispatch delivery for step 0
        if (steps.Count > 0)
        {
            await deliveryService.DispatchAsync(instance.Id, 0, payload, ct);
        }

        logger.LogInformation(
            "Alert instance {InstanceId} created for excursion {ExcursionId}, rule {RuleName}",
            instance.Id, excursionId, rule.Name);
    }

    private async Task HandleExcursionClosed(
        ExcursionTransition transition,
        Guid tenantId,
        NocturneDbContext db,
        CancellationToken ct)
    {
        if (!transition.ExcursionId.HasValue) return;

        var excursionId = transition.ExcursionId.Value;
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Resolve instances for this excursion
        var instances = await db.AlertInstances
            .Where(i => i.AlertExcursionId == excursionId && i.ResolvedAt == null)
            .ToListAsync(ct);

        foreach (var instance in instances)
        {
            instance.Status = "resolved";
            instance.ResolvedAt = now;
            instance.NextEscalationAt = null;
        }

        // Cancel pending deliveries
        var pendingDeliveries = await db.AlertDeliveries
            .Where(d => d.AlertInstanceId != Guid.Empty
                && instances.Select(i => i.Id).Contains(d.AlertInstanceId)
                && d.Status == "pending")
            .ToListAsync(ct);

        foreach (var delivery in pendingDeliveries)
        {
            delivery.Status = "expired";
        }

        await db.SaveChangesAsync(ct);

        try
        {
            await broadcastService.BroadcastAlertEventAsync("alert_resolved", new
            {
                excursionId,
                tenantId,
                resolvedAt = now,
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast alert_resolved for excursion {ExcursionId}", excursionId);
        }

        logger.LogInformation("Excursion {ExcursionId} resolved, {Count} instances closed", excursionId, instances.Count);
    }

    private async Task HandleExcursionContinues(
        ExcursionTransition transition,
        Guid tenantId,
        NocturneDbContext db,
        CancellationToken ct)
    {
        if (!transition.ExcursionId.HasValue) return;

        // Check for event-driven escalation advancement
        var now = timeProvider.GetUtcNow().UtcDateTime;

        var instances = await db.AlertInstances
            .Where(i => i.AlertExcursionId == transition.ExcursionId.Value
                && i.Status == "escalating"
                && i.NextEscalationAt != null
                && i.NextEscalationAt <= now)
            .ToListAsync(ct);

        foreach (var instance in instances)
        {
            await AdvanceEscalationAsync(instance, tenantId, db, now, ct);
        }
    }

    internal static async Task AdvanceEscalationAsync(
        AlertInstanceEntity instance,
        Guid tenantId,
        NocturneDbContext db,
        DateTime now,
        CancellationToken ct,
        IAlertDeliveryService? deliveryService = null,
        ILogger? logger = null)
    {
        var nextStepOrder = instance.CurrentStepOrder + 1;

        var steps = await db.AlertEscalationSteps
            .AsNoTracking()
            .Where(s => s.AlertScheduleId == instance.AlertScheduleId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(ct);

        var nextStep = steps.FirstOrDefault(s => s.StepOrder == nextStepOrder);
        if (nextStep is null)
        {
            // No more steps; stay at current step, stop escalating
            instance.Status = "triggered";
            instance.NextEscalationAt = null;
            await db.SaveChangesAsync(ct);
            return;
        }

        instance.CurrentStepOrder = nextStepOrder;

        var followingStep = steps.FirstOrDefault(s => s.StepOrder == nextStepOrder + 1);
        instance.NextEscalationAt = followingStep is not null
            ? now.AddSeconds(nextStep.DelaySeconds)
            : null;

        if (followingStep is null)
        {
            // This is the last step
            instance.Status = "triggered";
        }

        await db.SaveChangesAsync(ct);

        if (deliveryService is not null)
        {
            // Build a minimal payload for escalation dispatch
            var excursion = await db.AlertExcursions
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == instance.AlertExcursionId, ct);

            var rule = excursion is not null
                ? await db.AlertRules.AsNoTracking().FirstOrDefaultAsync(r => r.Id == excursion.AlertRuleId, ct)
                : null;

            var activeExcursionCount = await db.AlertExcursions
                .CountAsync(e => e.TenantId == tenantId && e.EndedAt == null, ct);

            var tenant = await db.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new { t.SubjectName, t.DisplayName })
                .FirstOrDefaultAsync(ct);

            var payload = new AlertPayload
            {
                AlertType = rule?.ConditionType ?? "unknown",
                RuleName = rule?.Name ?? "Unknown Rule",
                GlucoseValue = null,
                Trend = null,
                TrendRate = null,
                ReadingTimestamp = now,
                ExcursionId = instance.AlertExcursionId,
                InstanceId = instance.Id,
                TenantId = tenantId,
                SubjectName = tenant?.SubjectName ?? tenant?.DisplayName ?? "Unknown",
                ActiveExcursionCount = activeExcursionCount,
            };

            await deliveryService.DispatchAsync(instance.Id, nextStepOrder, payload, ct);
        }

        logger?.LogInformation(
            "Escalated instance {InstanceId} to step {StepOrder}",
            instance.Id, nextStepOrder);
    }
}

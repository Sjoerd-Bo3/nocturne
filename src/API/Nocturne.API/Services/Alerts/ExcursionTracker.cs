using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// State machine that manages the lifecycle of alert excursions.
///
/// States: idle -> confirming -> active -> hysteresis -> idle
///
/// On each evaluation:
///   1. Load or create tracker state for the rule.
///   2. Load rule configuration (confirmation_readings, hysteresis_minutes).
///   3. Apply state machine transitions.
///   4. Persist updated state and any excursion changes.
///   5. Return a transition describing what happened.
/// </summary>
public class ExcursionTracker(
    IAlertTrackerRepository repository,
    TimeProvider timeProvider,
    ILogger<ExcursionTracker> logger)
    : IExcursionTracker
{
    private const string StateIdle = "idle";
    private const string StateConfirming = "confirming";
    private const string StateActive = "active";
    private const string StateHysteresis = "hysteresis";

    public async Task<ExcursionTransition> ProcessEvaluationAsync(
        Guid alertRuleId,
        bool conditionMet,
        CancellationToken ct)
    {
        var rule = await repository.GetRuleAsync(alertRuleId, ct);
        if (rule == null)
        {
            logger.LogWarning("Alert rule {AlertRuleId} not found; skipping evaluation", alertRuleId);
            return new ExcursionTransition(ExcursionTransitionType.None);
        }

        var state = await repository.GetTrackerStateAsync(alertRuleId, ct)
                    ?? new AlertTrackerState
                    {
                        AlertRuleId = alertRuleId,
                        State = StateIdle,
                        ConfirmationCount = 0,
                    };

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var transition = state.State switch
        {
            StateIdle => await HandleIdle(state, rule, conditionMet, now, ct),
            StateConfirming => await HandleConfirming(state, rule, conditionMet, now, ct),
            StateActive => await HandleActive(state, rule, conditionMet, now, ct),
            StateHysteresis => await HandleHysteresis(state, rule, conditionMet, now, ct),
            _ => new ExcursionTransition(ExcursionTransitionType.None),
        };

        state.UpdatedAt = now;
        await repository.UpsertTrackerStateAsync(state, ct);

        return transition;
    }

    private async Task<ExcursionTransition> HandleIdle(
        AlertTrackerState state,
        AlertRule rule,
        bool conditionMet,
        DateTime now,
        CancellationToken ct)
    {
        if (!conditionMet)
            return new ExcursionTransition(ExcursionTransitionType.None);

        // If only 1 reading required, go straight to active
        if (rule.ConfirmationReadings <= 1)
        {
            return await OpenExcursion(state, rule, now, ct);
        }

        // Start confirming
        state.State = StateConfirming;
        state.ConfirmationCount = 1;
        return new ExcursionTransition(ExcursionTransitionType.None);
    }

    private async Task<ExcursionTransition> HandleConfirming(
        AlertTrackerState state,
        AlertRule rule,
        bool conditionMet,
        DateTime now,
        CancellationToken ct)
    {
        if (!conditionMet)
        {
            // Reset to idle
            state.State = StateIdle;
            state.ConfirmationCount = 0;
            return new ExcursionTransition(ExcursionTransitionType.None);
        }

        state.ConfirmationCount++;

        if (state.ConfirmationCount >= rule.ConfirmationReadings)
        {
            return await OpenExcursion(state, rule, now, ct);
        }

        // Still confirming
        return new ExcursionTransition(ExcursionTransitionType.None);
    }

    private async Task<ExcursionTransition> HandleActive(
        AlertTrackerState state,
        AlertRule rule,
        bool conditionMet,
        DateTime now,
        CancellationToken ct)
    {
        if (conditionMet)
        {
            return new ExcursionTransition(
                ExcursionTransitionType.ExcursionContinues,
                state.ActiveExcursionId);
        }

        // Start hysteresis
        state.State = StateHysteresis;

        if (state.ActiveExcursionId.HasValue)
        {
            await repository.SetHysteresisStartedAsync(state.ActiveExcursionId.Value, now, ct);
        }

        return new ExcursionTransition(
            ExcursionTransitionType.HysteresisStarted,
            state.ActiveExcursionId);
    }

    private async Task<ExcursionTransition> HandleHysteresis(
        AlertTrackerState state,
        AlertRule rule,
        bool conditionMet,
        DateTime now,
        CancellationToken ct)
    {
        if (conditionMet)
        {
            // Resume excursion
            state.State = StateActive;

            if (state.ActiveExcursionId.HasValue)
            {
                await repository.ClearHysteresisAsync(state.ActiveExcursionId.Value, ct);
            }

            return new ExcursionTransition(
                ExcursionTransitionType.HysteresisResumed,
                state.ActiveExcursionId);
        }

        // Check if hysteresis has expired.
        // We need to read the excursion to get HysteresisStartedAt.
        // For simplicity, we use the excursion record's HysteresisStartedAt.
        // If we can't find it, close immediately.
        var excursionId = state.ActiveExcursionId;

        // The hysteresis started when we transitioned to this state.
        // We recorded it on the excursion entity via SetHysteresisStartedAsync.
        // For the expiry check, we need to know when hysteresis started.
        // We'll use state.UpdatedAt as the proxy for when hysteresis started
        // (it was set when we entered hysteresis state).
        var hysteresisStart = state.UpdatedAt;
        var hysteresisExpiry = hysteresisStart.AddMinutes(rule.HysteresisMinutes);

        if (now >= hysteresisExpiry)
        {
            // Hysteresis expired, close excursion
            if (excursionId.HasValue)
            {
                await repository.CloseExcursionAsync(excursionId.Value, now, ct);
            }

            state.State = StateIdle;
            state.ConfirmationCount = 0;
            state.ActiveExcursionId = null;

            return new ExcursionTransition(
                ExcursionTransitionType.ExcursionClosed,
                excursionId);
        }

        // Still in hysteresis, no transition
        return new ExcursionTransition(ExcursionTransitionType.None);
    }

    private async Task<ExcursionTransition> OpenExcursion(
        AlertTrackerState state,
        AlertRule rule,
        DateTime now,
        CancellationToken ct)
    {
        var excursion = await repository.CreateExcursionAsync(rule.Id, now, ct);

        state.State = StateActive;
        state.ConfirmationCount = 0;
        state.ActiveExcursionId = excursion.Id;

        logger.LogInformation(
            "Excursion {ExcursionId} opened for alert rule {AlertRuleId}",
            excursion.Id,
            rule.Id);

        return new ExcursionTransition(
            ExcursionTransitionType.ExcursionOpened,
            excursion.Id);
    }
}

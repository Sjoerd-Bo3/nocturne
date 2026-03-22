namespace Nocturne.Core.Contracts.Alerts;

public enum ExcursionTransitionType
{
    None,
    ExcursionOpened,
    ExcursionContinues,
    HysteresisStarted,
    HysteresisResumed,
    ExcursionClosed
}

public record ExcursionTransition(ExcursionTransitionType Type, Guid? ExcursionId = null);

public interface IExcursionTracker
{
    Task<ExcursionTransition> ProcessEvaluationAsync(Guid alertRuleId, bool conditionMet, CancellationToken ct);
}

using Nocturne.Core.Contracts.Alerts;

namespace Nocturne.API.Services.Alerts.Evaluators;

/// <summary>
/// Resolves a condition type string to the corresponding <see cref="IConditionEvaluator"/>.
/// Registered as singleton; constructor takes all registered evaluators via DI.
/// </summary>
public class ConditionEvaluatorRegistry
{
    private readonly Dictionary<string, IConditionEvaluator> _evaluators;

    public ConditionEvaluatorRegistry(IEnumerable<IConditionEvaluator> evaluators)
    {
        _evaluators = evaluators.ToDictionary(e => e.ConditionType, e => e);
    }

    public IConditionEvaluator? GetEvaluator(string conditionType)
    {
        _evaluators.TryGetValue(conditionType, out var evaluator);
        return evaluator;
    }
}

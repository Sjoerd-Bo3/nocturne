using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

public interface IConditionEvaluator
{
    string ConditionType { get; }
    bool Evaluate(string conditionParamsJson, SensorContext context);
}

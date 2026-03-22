using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Evaluators;

public class ThresholdEvaluator : IConditionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public string ConditionType => "threshold";

    public bool Evaluate(string conditionParamsJson, SensorContext context)
    {
        if (context.LatestValue is null)
            return false;

        var condition = JsonSerializer.Deserialize<ThresholdCondition>(conditionParamsJson, JsonOptions);
        if (condition is null)
            return false;

        return condition.Direction.ToLowerInvariant() switch
        {
            "below" => context.LatestValue.Value < condition.Value,
            "above" => context.LatestValue.Value > condition.Value,
            _ => false
        };
    }
}

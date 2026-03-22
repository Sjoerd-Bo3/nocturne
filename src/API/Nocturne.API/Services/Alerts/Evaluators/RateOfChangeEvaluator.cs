using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Evaluators;

public class RateOfChangeEvaluator : IConditionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public string ConditionType => "rate_of_change";

    public bool Evaluate(string conditionParamsJson, SensorContext context)
    {
        if (context.TrendRate is null)
            return false;

        var condition = JsonSerializer.Deserialize<RateOfChangeCondition>(conditionParamsJson, JsonOptions);
        if (condition is null)
            return false;

        return condition.Direction.ToLowerInvariant() switch
        {
            // A "falling" rate of 3.0 means trigger when the actual rate <= -3.0
            "falling" => context.TrendRate.Value <= -condition.Rate,
            // A "rising" rate of 3.0 means trigger when the actual rate >= 3.0
            "rising" => context.TrendRate.Value >= condition.Rate,
            _ => false
        };
    }
}

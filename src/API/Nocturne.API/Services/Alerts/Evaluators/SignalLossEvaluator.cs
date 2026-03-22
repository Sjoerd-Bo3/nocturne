using System.Text.Json;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Evaluators;

public class SignalLossEvaluator : IConditionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly TimeProvider _timeProvider;

    public SignalLossEvaluator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public string ConditionType => "signal_loss";

    public bool Evaluate(string conditionParamsJson, SensorContext context)
    {
        // No data at all means signal is lost
        if (context.LastReadingAt is null)
            return true;

        var condition = JsonSerializer.Deserialize<SignalLossCondition>(conditionParamsJson, JsonOptions);
        if (condition is null)
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var elapsed = now - context.LastReadingAt.Value;

        return elapsed > TimeSpan.FromMinutes(condition.TimeoutMinutes);
    }
}

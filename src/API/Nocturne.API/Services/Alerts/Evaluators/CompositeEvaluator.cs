using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Evaluators;

public class CompositeEvaluator : IConditionEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly IServiceProvider _serviceProvider;
    private ConditionEvaluatorRegistry? _registry;

    public CompositeEvaluator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private ConditionEvaluatorRegistry Registry =>
        _registry ??= _serviceProvider.GetRequiredService<ConditionEvaluatorRegistry>();

    public string ConditionType => "composite";

    public bool Evaluate(string conditionParamsJson, SensorContext context)
    {
        var condition = JsonSerializer.Deserialize<CompositeCondition>(conditionParamsJson, JsonOptions);
        if (condition is null || condition.Conditions.Count == 0)
            return false;

        return condition.Operator.ToLowerInvariant() switch
        {
            "and" => condition.Conditions.All(node => EvaluateNode(node, context)),
            "or" => condition.Conditions.Any(node => EvaluateNode(node, context)),
            _ => false
        };
    }

    private bool EvaluateNode(ConditionNode node, SensorContext context)
    {
        var evaluator = Registry.GetEvaluator(node.Type);
        if (evaluator is null)
            return false;

        // Serialize the appropriate sub-condition back to JSON for the evaluator
        var paramsJson = node.Type.ToLowerInvariant() switch
        {
            "threshold" => JsonSerializer.Serialize(node.Threshold, JsonOptions),
            "rate_of_change" => JsonSerializer.Serialize(node.RateOfChange, JsonOptions),
            "signal_loss" => JsonSerializer.Serialize(node.SignalLoss, JsonOptions),
            "composite" => JsonSerializer.Serialize(node.Composite, JsonOptions),
            _ => "{}"
        };

        return evaluator.Evaluate(paramsJson, context);
    }
}

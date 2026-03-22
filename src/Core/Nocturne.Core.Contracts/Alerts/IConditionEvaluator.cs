using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

/// <summary>
/// Pure-function evaluator that determines whether a single condition type
/// is met given the current sensor data.
/// </summary>
public interface IConditionEvaluator
{
    /// <summary>
    /// Discriminator value that matches the JSON "type" field
    /// (e.g. "threshold", "rate_of_change", "signal_loss", "composite").
    /// </summary>
    string ConditionType { get; }

    /// <summary>
    /// Evaluate the condition against the current sensor context.
    /// </summary>
    /// <param name="conditionParamsJson">JSON string containing the condition parameters.</param>
    /// <param name="context">Current sensor snapshot.</param>
    /// <returns>True if the condition is met (alert should fire).</returns>
    bool Evaluate(string conditionParamsJson, SensorContext context);
}

using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

/// <summary>
/// Top-level orchestrator for the alert pipeline. Evaluates all enabled
/// <see cref="AlertRule"/> instances against incoming sensor data and triggers
/// excursion tracking, escalation, and delivery as needed.
/// </summary>
/// <seealso cref="IExcursionTracker"/>
/// <seealso cref="IConditionEvaluator"/>
/// <seealso cref="IAlertDeliveryService"/>
public interface IAlertOrchestrator
{
    /// <summary>
    /// Evaluate all enabled rules for the current tenant against the latest sensor data.
    /// Called by the glucose ingest pipeline on each new reading.
    /// </summary>
    /// <param name="context">The current <see cref="SensorContext"/> containing the latest glucose reading and trend data.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when all rules have been evaluated and any resulting alerts dispatched.</returns>
    Task EvaluateAsync(SensorContext context, CancellationToken ct);
}

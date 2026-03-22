using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertOrchestrator
{
    /// <summary>
    /// Evaluate all enabled rules for the current tenant against the latest sensor data.
    /// Called by the glucose ingest pipeline on each new reading.
    /// </summary>
    Task EvaluateAsync(SensorContext context, CancellationToken ct);
}

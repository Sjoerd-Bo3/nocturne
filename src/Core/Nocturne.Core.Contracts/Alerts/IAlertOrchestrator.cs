using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertOrchestrator
{
    /// <summary>
    /// Evaluates V4 SensorGlucose records against alert rules and processes any triggered alerts.
    /// This is the primary path for all glucose alert evaluation.
    /// </summary>
    Task EvaluateAndProcessSensorGlucoseAsync(
        IEnumerable<SensorGlucose> readings,
        string? userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates legacy V1/V3 Entry records against alert rules.
    /// Converts entries to SensorGlucose internally before evaluation.
    /// </summary>
    Task EvaluateAndProcessEntriesAsync(
        IEnumerable<Entry> entries,
        string? userId,
        CancellationToken cancellationToken = default
    );
}

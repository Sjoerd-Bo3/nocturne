using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for alert tracker state and excursion persistence.
/// </summary>
public interface IAlertTrackerRepository
{
    /// <summary>
    /// Get the tracker state for a specific alert rule.
    /// </summary>
    Task<AlertTrackerState?> GetTrackerStateAsync(
        Guid alertRuleId,
        CancellationToken ct = default);

    /// <summary>
    /// Insert or update the tracker state for a rule.
    /// </summary>
    Task UpsertTrackerStateAsync(
        AlertTrackerState state,
        CancellationToken ct = default);

    /// <summary>
    /// Get the alert rule configuration.
    /// </summary>
    Task<AlertRule?> GetRuleAsync(
        Guid alertRuleId,
        CancellationToken ct = default);

    /// <summary>
    /// Create a new excursion record and return it.
    /// </summary>
    Task<AlertExcursion> CreateExcursionAsync(
        Guid alertRuleId,
        DateTime startedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Close an excursion by setting its EndedAt timestamp.
    /// </summary>
    Task CloseExcursionAsync(
        Guid excursionId,
        DateTime endedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Record the start of hysteresis on an excursion.
    /// </summary>
    Task SetHysteresisStartedAsync(
        Guid excursionId,
        DateTime hysteresisStartedAt,
        CancellationToken ct = default);

    /// <summary>
    /// Clear the hysteresis timestamp on an excursion (when resuming from hysteresis).
    /// </summary>
    Task ClearHysteresisAsync(
        Guid excursionId,
        CancellationToken ct = default);
}

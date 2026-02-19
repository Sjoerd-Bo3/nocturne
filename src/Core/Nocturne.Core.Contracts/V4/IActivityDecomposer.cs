using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4;

/// <summary>
/// Decomposes legacy Activity records into dedicated v4 models (HeartRate, StepCount).
/// Activities with "bpm" in AdditionalProperties are routed to heart_rates table.
/// Activities with "metric" in AdditionalProperties are routed to step_counts table.
/// Regular activities (exercise, sleep, etc.) pass through unchanged to StateSpan storage.
/// </summary>
public interface IActivityDecomposer
{
    /// <summary>
    /// Classifies an activity and routes it to the appropriate dedicated table if it is sensor data.
    /// Returns a result with created/updated records for heart rate or step count data.
    /// Returns an empty result for regular activities (caller should proceed with StateSpan storage).
    /// </summary>
    Task<DecompositionResult> DecomposeAsync(Activity activity, CancellationToken ct = default);

    /// <summary>
    /// Deletes all dedicated records that were decomposed from a legacy Activity with the given ID.
    /// </summary>
    /// <returns>Total number of records deleted across heart_rates and step_counts tables</returns>
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);

    /// <summary>
    /// Determines whether an activity represents heart rate data (has "bpm" in AdditionalProperties).
    /// </summary>
    bool IsHeartRate(Activity activity);

    /// <summary>
    /// Determines whether an activity represents step count data (has "metric" in AdditionalProperties).
    /// </summary>
    bool IsStepCount(Activity activity);

    /// <summary>
    /// Returns true for heart rate or step count data that should be routed to a dedicated table
    /// rather than stored as a StateSpan.
    /// </summary>
    bool IsSensorData(Activity activity);
}

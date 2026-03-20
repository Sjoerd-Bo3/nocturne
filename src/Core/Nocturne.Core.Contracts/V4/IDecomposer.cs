using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4;

/// <summary>
/// Unified generic interface for decomposing legacy records into v4 granular models.
/// Implemented by each typed decomposer alongside its specific interface.
/// </summary>
/// <typeparam name="T">The legacy domain model type (Entry, Treatment, DeviceStatus, Activity, Profile)</typeparam>
public interface IDecomposer<in T> where T : class
{
    Task<DecompositionResult> DecomposeAsync(T record, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
}

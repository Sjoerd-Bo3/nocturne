using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4;

/// <summary>
/// Unified orchestration layer for decomposing legacy records into v4 models.
/// Dispatches to the appropriate <see cref="IDecomposer{T}"/> and absorbs errors internally.
/// </summary>
public interface IDecompositionPipeline
{
    Task<BatchDecompositionResult> DecomposeAsync<T>(IEnumerable<T> records, CancellationToken ct = default) where T : class;
    Task<BatchDecompositionResult> DecomposeAsync<T>(T record, CancellationToken ct = default) where T : class;
    Task<int> DeleteByLegacyIdAsync<T>(string legacyId, CancellationToken ct = default) where T : class;
}

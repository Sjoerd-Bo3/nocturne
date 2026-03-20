using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Driven port for treatment-specific caching. The adapter owns cache key
/// construction, TTL policy, and demo-mode isolation.
/// </summary>
public interface ITreatmentCache
{
    /// <summary>
    /// Get cached treatments for the given query, or compute and cache them.
    /// Returns null if the query is not cacheable, signaling the caller to
    /// go directly to the store.
    /// </summary>
    Task<IReadOnlyList<Treatment>?> GetOrComputeAsync(
        TreatmentQuery query,
        Func<Task<IReadOnlyList<Treatment>>> compute,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached treatment data for the current tenant.
    /// </summary>
    Task InvalidateAsync(CancellationToken ct = default);
}

using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Entries;

/// <summary>
/// Driven port for entry-specific caching. The adapter owns cache key
/// construction, TTL policy, and demo-mode isolation.
/// </summary>
public interface IEntryCache
{
    /// <summary>
    /// Get cached entries for the given query, or compute and cache them.
    /// Returns null if the query is not cacheable, signaling the caller to
    /// go directly to the store.
    /// </summary>
    Task<IReadOnlyList<Entry>?> GetOrComputeAsync(
        EntryQuery query,
        Func<Task<IReadOnlyList<Entry>>> compute,
        CancellationToken ct = default);

    /// <summary>
    /// Get cached current entry, or compute and cache it.
    /// </summary>
    Task<Entry?> GetOrComputeCurrentAsync(
        Func<Task<Entry?>> compute,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate all cached entry data for the current tenant.
    /// </summary>
    Task InvalidateAsync(CancellationToken ct = default);
}

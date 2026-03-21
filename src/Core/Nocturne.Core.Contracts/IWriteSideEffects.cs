namespace Nocturne.Core.Contracts;

/// <summary>
/// Orchestrates post-write side effects for data services: cache invalidation,
/// SignalR broadcasting, and V4 decomposition. All effects are fire-and-forget —
/// errors are caught and logged, never propagated to callers.
/// </summary>
public interface IWriteSideEffects
{
    /// <summary>
    /// Run all post-create side effects: invalidate cache, broadcast storage-create,
    /// optionally broadcast data-update, and decompose into V4 tables.
    /// </summary>
    Task OnCreatedAsync<T>(
        string collectionName,
        IReadOnlyList<T> records,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class;

    /// <summary>
    /// Run all post-update side effects: invalidate cache, broadcast storage-update,
    /// and re-decompose into V4 tables.
    /// </summary>
    Task OnUpdatedAsync<T>(
        string collectionName,
        T record,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class;

    /// <summary>
    /// Run pre-delete cleanup: remove V4 records by legacy ID before the DB delete.
    /// Call this BEFORE the repository delete.
    /// </summary>
    Task BeforeDeleteAsync<T>(
        string legacyId,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class;

    /// <summary>
    /// Run all post-delete side effects: invalidate cache and broadcast storage-delete.
    /// Call this AFTER the repository delete succeeds.
    /// </summary>
    Task OnDeletedAsync<T>(
        string collectionName,
        T? deletedRecord,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class;

    /// <summary>
    /// Run post-bulk-delete side effects: invalidate cache and broadcast aggregate delete count.
    /// </summary>
    Task OnBulkDeletedAsync(
        string collectionName,
        long deletedCount,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Immutable configuration for which side effects to run and how.
/// Construct as static readonly per service — these never change at runtime.
/// </summary>
public sealed record WriteEffectOptions
{
    /// <summary>Exact cache keys to remove after a write. Evaluated eagerly.</summary>
    public IReadOnlyList<string> CacheKeysToRemove { get; init; } = [];

    /// <summary>Cache key patterns to remove via wildcard match after a write.</summary>
    public IReadOnlyList<string> CachePatternsToClear { get; init; } = [];

    /// <summary>Whether to run IDecompositionPipeline after create/update.</summary>
    public bool DecomposeToV4 { get; init; }

    /// <summary>Whether to also call BroadcastDataUpdateAsync after create (Entry glucose only).</summary>
    public bool BroadcastDataUpdate { get; init; }
}

using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4;
using Nocturne.Infrastructure.Cache.Abstractions;

namespace Nocturne.API.Services;

public class WriteSideEffectsService : IWriteSideEffects
{
    private readonly ICacheService _cache;
    private readonly ISignalRBroadcastService _broadcast;
    private readonly IDecompositionPipeline _pipeline;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly IReadOnlyDictionary<string, ICollectionEffectDescriptor> _descriptors;
    private readonly ILogger<WriteSideEffectsService> _logger;

    public WriteSideEffectsService(
        ICacheService cache,
        ISignalRBroadcastService broadcast,
        IDecompositionPipeline pipeline,
        ITenantAccessor tenantAccessor,
        IEnumerable<ICollectionEffectDescriptor> descriptors,
        ILogger<WriteSideEffectsService> logger
    )
    {
        _cache = cache;
        _broadcast = broadcast;
        _pipeline = pipeline;
        _tenantAccessor = tenantAccessor;
        _descriptors = descriptors.ToDictionary(
            d => d.CollectionName,
            StringComparer.OrdinalIgnoreCase
        );
        _logger = logger;
    }

    private WriteEffectOptions ResolveOptions(
        string collectionName,
        WriteEffectOptions? explicitOptions
    )
    {
        if (explicitOptions is not null)
            return explicitOptions;
        if (!_descriptors.TryGetValue(collectionName, out var descriptor))
            return new WriteEffectOptions();

        var tenantId = _tenantAccessor.Context?.TenantId.ToString() ?? "";
        return new WriteEffectOptions
        {
            CacheKeysToRemove = descriptor.GetCacheKeysToRemove(tenantId),
            CachePatternsToClear = descriptor.GetCachePatternsToClear(tenantId),
            DecomposeToV4 = descriptor.DecomposeToV4,
            BroadcastDataUpdate = descriptor.BroadcastDataUpdateOnCreate,
        };
    }

    public async Task OnCreatedAsync<T>(
        string collectionName,
        IReadOnlyList<T> records,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        options = ResolveOptions(collectionName, options);

        await InvalidateCacheAsync(options, cancellationToken);

        try
        {
            foreach (var record in records)
            {
                await _broadcast.BroadcastStorageCreateAsync(
                    collectionName,
                    new { colName = collectionName, doc = record }
                );
            }

            if (options.BroadcastDataUpdate)
            {
                await _broadcast.BroadcastDataUpdateAsync(records.ToArray());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Broadcast failed during create for {Collection}",
                collectionName
            );
        }

        if (options.DecomposeToV4)
        {
            await _pipeline.DecomposeAsync((IEnumerable<T>)records, cancellationToken);
        }
    }

    public async Task OnUpdatedAsync<T>(
        string collectionName,
        T record,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        options = ResolveOptions(collectionName, options);

        await InvalidateCacheAsync(options, cancellationToken);

        try
        {
            await _broadcast.BroadcastStorageUpdateAsync(
                collectionName,
                new { colName = collectionName, doc = record }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Broadcast failed during update for {Collection}",
                collectionName
            );
        }

        if (options.DecomposeToV4)
        {
            await _pipeline.DecomposeAsync(record, cancellationToken);
        }
    }

    public async Task BeforeDeleteAsync<T>(
        string legacyId,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        options ??= new WriteEffectOptions();

        if (options.DecomposeToV4)
        {
            await _pipeline.DeleteByLegacyIdAsync<T>(legacyId, cancellationToken);
        }
    }

    public async Task OnDeletedAsync<T>(
        string collectionName,
        T? deletedRecord,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    ) where T : class
    {
        options = ResolveOptions(collectionName, options);

        await InvalidateCacheAsync(options, cancellationToken);

        if (deletedRecord is null)
            return;

        try
        {
            await _broadcast.BroadcastStorageDeleteAsync(
                collectionName,
                new { colName = collectionName, doc = deletedRecord }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Broadcast failed during delete for {Collection}",
                collectionName
            );
        }
    }

    public async Task OnBulkDeletedAsync(
        string collectionName,
        long deletedCount,
        WriteEffectOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        if (deletedCount <= 0)
            return;

        options = ResolveOptions(collectionName, options);

        await InvalidateCacheAsync(options, cancellationToken);

        try
        {
            await _broadcast.BroadcastStorageDeleteAsync(
                collectionName,
                new { colName = collectionName, deletedCount }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Broadcast failed during bulk delete for {Collection}",
                collectionName
            );
        }
    }

    private async Task InvalidateCacheAsync(WriteEffectOptions options, CancellationToken ct)
    {
        try
        {
            foreach (var key in options.CacheKeysToRemove)
            {
                await _cache.RemoveAsync(key, ct);
            }

            foreach (var pattern in options.CachePatternsToClear)
            {
                await _cache.RemoveByPatternAsync(pattern, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed");
        }
    }
}

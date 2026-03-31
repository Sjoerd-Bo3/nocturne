using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Keys;

namespace Nocturne.API.Services.Entries;

/// <summary>
/// Entry event sink that translates write events into cache invalidation
/// and SignalR broadcasts via IWriteSideEffects.
/// </summary>
public class SignalREntryEventSink : IDataEventSink<Entry>
{
    private readonly IWriteSideEffects _sideEffects;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<SignalREntryEventSink> _logger;
    private const string CollectionName = "entries";

    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public SignalREntryEventSink(
        IWriteSideEffects sideEffects,
        ITenantAccessor tenantAccessor,
        ILogger<SignalREntryEventSink> logger)
    {
        _sideEffects = sideEffects;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    private WriteEffectOptions BuildWriteOptions() => new()
    {
        CacheKeysToRemove = [CacheKeyBuilder.BuildCurrentEntriesKey(TenantCacheId)],
        CachePatternsToClear = [CacheKeyBuilder.BuildRecentEntriesPattern(TenantCacheId)],
        DecomposeToV4 = true,
        BroadcastDataUpdate = true,
    };

    private WriteEffectOptions BuildWriteOptionsNoBroadcastDataUpdate() => new()
    {
        CacheKeysToRemove = [CacheKeyBuilder.BuildCurrentEntriesKey(TenantCacheId)],
        CachePatternsToClear = [CacheKeyBuilder.BuildRecentEntriesPattern(TenantCacheId)],
        DecomposeToV4 = true,
        BroadcastDataUpdate = false,
    };

    private WriteEffectOptions BuildCacheOnlyOptions() => new()
    {
        CacheKeysToRemove = [CacheKeyBuilder.BuildCurrentEntriesKey(TenantCacheId)],
        CachePatternsToClear = [CacheKeyBuilder.BuildRecentEntriesPattern(TenantCacheId)],
    };

    public async Task OnCreatedAsync(IReadOnlyList<Entry> entries, CancellationToken ct = default)
    {
        try
        {
            await _sideEffects.OnCreatedAsync(CollectionName, entries, BuildWriteOptions(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process create side effects for {Count} entries", entries.Count);
        }
    }

    public async Task OnUpdatedAsync(Entry entry, CancellationToken ct = default)
    {
        try
        {
            await _sideEffects.OnUpdatedAsync(CollectionName, entry, BuildWriteOptionsNoBroadcastDataUpdate(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process update side effects for entry {Id}", entry.Id);
        }
    }

    public async Task BeforeDeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _sideEffects.BeforeDeleteAsync<Entry>(id, BuildWriteOptions(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pre-delete side effects for entry {Id}", id);
        }
    }

    public async Task OnDeletedAsync(Entry? entry, CancellationToken ct = default)
    {
        try
        {
            await _sideEffects.OnDeletedAsync(CollectionName, entry, BuildWriteOptions(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process delete side effects for entry {Id}", entry?.Id);
        }
    }

    public async Task OnBulkDeletedAsync(long deletedCount, CancellationToken ct = default)
    {
        try
        {
            await _sideEffects.OnBulkDeletedAsync(CollectionName, deletedCount, BuildCacheOnlyOptions(), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process bulk delete side effects for {Count} entries", deletedCount);
        }
    }
}

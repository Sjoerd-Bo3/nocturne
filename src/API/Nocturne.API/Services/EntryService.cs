using System.Text.Json;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Constants;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for entry operations with WebSocket broadcasting
/// </summary>
public class EntryService : IEntryService
{
    private readonly IEntryRepository _entries;
    private readonly IWriteSideEffects _sideEffects;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly IDemoModeService _demoModeService;
    private readonly IV4ToLegacyProjectionService _projectionService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<EntryService> _logger;
    private const string CollectionName = "entries";
    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public EntryService(
        IEntryRepository entries,
        IWriteSideEffects sideEffects,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        IDemoModeService demoModeService,
        IV4ToLegacyProjectionService projectionService,
        ITenantAccessor tenantAccessor,
        ILogger<EntryService> logger
    )
    {
        _entries = entries;
        _sideEffects = sideEffects;
        _cacheService = cacheService;
        _cacheConfig = cacheConfig.Value;
        _demoModeService = demoModeService;
        _projectionService = projectionService;
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

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        var actualCount = count ?? 10;
        var actualSkip = skip ?? 0;

        // Build query with demo mode filter at database level
        var findQuery = BuildDemoModeFilterQuery(find);

        // Parse time range from find query for V4 projection
        var (fromMills, toMills) = ParseTimeRangeFromFind(find);

        // Cache recent entries for common queries (skip = 0 and common counts)
        // Include demo mode in cache key to avoid mixing demo/non-demo data
        if (actualSkip == 0 && IsCommonEntryCount(actualCount))
        {
            var demoSuffix = _demoModeService.IsEnabled ? ":demo" : "";
            var cacheKey =
                CacheKeyBuilder.BuildRecentEntriesKey(TenantCacheId, actualCount, find)
                + demoSuffix;
            var cacheTtl = TimeSpan.FromSeconds(
                CacheConstants.Defaults.RecentEntriesExpirationSeconds
            );

            var legacyEntries = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent entries (count: {Count}, type: {Type}, demoMode: {DemoMode}), fetching from database with filter: {Filter}",
                        actualCount,
                        find ?? "all",
                        _demoModeService.IsEnabled,
                        findQuery
                    );
                    var entries = await _entries.GetEntriesWithAdvancedFilterAsync(
                        type: "sgv", // Default to SGV entries
                        count: actualCount,
                        skip: actualSkip,
                        findQuery: findQuery,
                        cancellationToken: cancellationToken
                    );
                    return entries.ToList();
                },
                cacheTtl,
                cancellationToken
            );

            // Add V4-only projected entries (not cached – projection is a live query)
            var projectedEntries = await _projectionService.GetProjectedEntriesAsync(
                fromMills,
                toMills,
                actualCount,
                0,
                descending: true,
                cancellationToken
            );

            return MergeAndDeduplicateEntries(legacyEntries, projectedEntries, actualCount, actualSkip);
        }

        // Non-cached path for non-standard queries — fetch from skip=0 so the merge can
        // correctly interleave legacy and projected entries before applying the final skip.
        var allLegacyEntries = await _entries.GetEntriesWithAdvancedFilterAsync(
            type: "sgv", // Default to SGV entries
            count: actualCount + actualSkip,
            skip: 0,
            findQuery: findQuery,
            cancellationToken: cancellationToken
        );

        var allProjectedEntries = await _projectionService.GetProjectedEntriesAsync(
            fromMills,
            toMills,
            actualCount + actualSkip,
            0,
            descending: true,
            cancellationToken
        );

        return MergeAndDeduplicateEntries(allLegacyEntries, allProjectedEntries, actualCount, actualSkip);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type,
        int count,
        int skip,
        CancellationToken cancellationToken
    )
    {
        // Build query with demo mode filter at database level
        var findQuery = BuildDemoModeFilterQuery(null);

        // Only project SGV entries – SensorGlucose maps to type "sgv"
        var shouldProject = string.IsNullOrEmpty(type) || type == "sgv";

        // Cache recent entries for common queries (skip = 0 and common counts)
        // Include demo mode in cache key to avoid mixing demo/non-demo data
        if (skip == 0 && IsCommonEntryCount(count))
        {
            var demoSuffix = _demoModeService.IsEnabled ? ":demo" : "";
            var cacheKey =
                CacheKeyBuilder.BuildRecentEntriesKey(TenantCacheId, count, type) + demoSuffix;
            var cacheTtl = TimeSpan.FromSeconds(
                CacheConstants.Defaults.RecentEntriesExpirationSeconds
            );

            var legacyEntries = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent entries (count: {Count}, type: {Type}, demoMode: {DemoMode}), fetching from database with filter: {Filter}",
                        count,
                        type ?? "all",
                        _demoModeService.IsEnabled,
                        findQuery
                    );
                    var entries = await _entries.GetEntriesWithAdvancedFilterAsync(
                        type,
                        count,
                        skip,
                        findQuery,
                        cancellationToken: cancellationToken
                    );
                    return entries.ToList();
                },
                cacheTtl,
                cancellationToken
            );

            if (!shouldProject)
                return legacyEntries;

            var projectedEntries = await _projectionService.GetProjectedEntriesAsync(
                fromMills: null,
                toMills: null,
                limit: count,
                offset: 0,
                descending: true,
                cancellationToken
            );

            return MergeAndDeduplicateEntries(legacyEntries, projectedEntries, count, skip);
        }

        // Non-cached path for non-standard queries — fetch from skip=0 so the merge can
        // correctly interleave legacy and projected entries before applying the final skip.
        var allLegacyEntries = await _entries.GetEntriesWithAdvancedFilterAsync(
            type,
            count + skip,
            0,
            findQuery,
            cancellationToken: cancellationToken
        );

        if (!shouldProject)
            return allLegacyEntries.Skip(skip).Take(count);

        var allProjectedEntries = await _projectionService.GetProjectedEntriesAsync(
            fromMills: null,
            toMills: null,
            limit: count + skip,
            offset: 0,
            descending: true,
            cancellationToken
        );

        return MergeAndDeduplicateEntries(allLegacyEntries, allProjectedEntries, count, skip);
    }

    /// <summary>
    /// Determines if the entry count is common enough to cache
    /// </summary>
    /// <param name="count">The count to check</param>
    /// <returns>True if the count is common (10, 50, 100), false otherwise</returns>
    private static bool IsCommonEntryCount(int count)
    {
        return count is 10 or 50 or 100;
    }

    /// <summary>
    /// Parse a time range from a MongoDB-style find query JSON string.
    /// Walks the document looking for numeric $gte / $lte values on any field.
    /// Returns (null, null) if the query is absent or contains no time constraints.
    /// </summary>
    private static (long? from, long? to) ParseTimeRangeFromFind(string? find)
    {
        if (string.IsNullOrEmpty(find))
            return (null, null);

        long? from = null;
        long? to = null;

        try
        {
            using var doc = JsonDocument.Parse(find);
            foreach (var field in doc.RootElement.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var op in field.Value.EnumerateObject())
                {
                    if (op.Value.ValueKind != JsonValueKind.Number)
                        continue;

                    if (op.Name == "$gte" && op.Value.TryGetInt64(out var gte))
                        from = gte;
                    else if (op.Name == "$lte" && op.Value.TryGetInt64(out var lte))
                        to = lte;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed query — projection will run without time bounds, which is safe.
        }

        return (from, to);
    }

    /// <summary>
    /// Merges legacy entries with V4-projected entries.
    /// Deduplication rule: if a legacy entry has an Id that matches a V4 record's LegacyId
    /// the V4 projection is discarded (the legacy entry is canonical).
    /// Since <see cref="V4ToLegacyProjectionService"/> only returns records with LegacyId == null,
    /// any Id from a V4-only record will not collide with legacy Ids.  This method therefore
    /// deduplicates by Mills to avoid timestamp duplicates from concurrent writes.
    /// </summary>
    private static IEnumerable<Entry> MergeAndDeduplicateEntries(
        IEnumerable<Entry> legacyEntries,
        IEnumerable<Entry> projectedEntries,
        int count,
        int skip
    )
    {
        // Build set of legacy entry IDs so we can skip V4 records that already have a legacy row.
        var legacyList = legacyEntries.ToList();
        var legacyIds = legacyList.Select(e => e.Id).Where(id => id != null).ToHashSet();

        // Build set of Mills from legacy entries for coarse dedup on timestamp.
        var legacyMillsSet = legacyList.Select(e => e.Mills).ToHashSet();

        var filteredProjected = projectedEntries
            .Where(p => !legacyIds.Contains(p.Id) && !legacyMillsSet.Contains(p.Mills));

        return legacyList
            .Concat(filteredProjected)
            .OrderByDescending(e => e.Mills)
            .Skip(skip)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Builds a find query that filters by demo mode.
    /// This ensures filtering happens at the database level for efficiency.
    /// </summary>
    /// <param name="existingQuery">Optional existing query to merge with</param>
    /// <returns>A JSON find query string with data_source filter</returns>
    private string BuildDemoModeFilterQuery(string? existingQuery = null)
    {
        // When demo mode is enabled, filter for data_source = "demo-service"
        // When demo mode is disabled, filter for data_source != "demo-service" (null or other sources)
        string demoFilter;
        if (_demoModeService.IsEnabled)
        {
            demoFilter = $"\"data_source\":\"{Core.Constants.DataSources.DemoService}\"";
        }
        else
        {
            // Use $ne operator to exclude demo service data
            demoFilter =
                $"\"data_source\":{{\"$ne\":\"{Core.Constants.DataSources.DemoService}\"}}";
        }

        if (string.IsNullOrWhiteSpace(existingQuery) || existingQuery == "{}")
        {
            return "{" + demoFilter + "}";
        }

        // Merge with existing query - insert demo filter into existing JSON object
        var trimmed = existingQuery.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner))
            {
                return "{" + demoFilter + "}";
            }
            return "{" + demoFilter + "," + inner + "}";
        }

        // If query doesn't look like JSON, just return demo filter
        return "{" + demoFilter + "}";
    }

    /// <summary>
    /// Filters entries based on demo mode status (legacy client-side filtering).
    /// This is kept for backward compatibility but database-level filtering is preferred.
    /// </summary>
    private IEnumerable<Entry> FilterEntriesByDemoMode(IEnumerable<Entry> entries)
    {
        var entriesList = entries.ToList();
        var demoEntries = entriesList
            .Where(e => e.DataSource == Core.Constants.DataSources.DemoService)
            .ToList();
        var nonDemoEntries = entriesList
            .Where(e => e.DataSource != Core.Constants.DataSources.DemoService)
            .ToList();

        _logger.LogDebug(
            "FilterEntriesByDemoMode: DemoModeEnabled={DemoMode}, TotalEntries={Total}, DemoEntries={Demo}, NonDemoEntries={NonDemo}",
            _demoModeService.IsEnabled,
            entriesList.Count,
            demoEntries.Count,
            nonDemoEntries.Count
        );

        if (_demoModeService.IsEnabled)
        {
            // In demo mode, ONLY return demo entries - never fall back to real data
            if (demoEntries.Count > 0)
            {
                _logger.LogDebug("Demo mode ON: Returning {Count} demo entries", demoEntries.Count);
                return demoEntries;
            }
            else
            {
                // No demo entries available - return empty to avoid exposing real data
                _logger.LogWarning(
                    "Demo mode is enabled but no demo entries found. Returning empty results. "
                        + "Ensure the Demo Service is running and generating data."
                );
                return Enumerable.Empty<Entry>();
            }
        }
        else
        {
            // Not in demo mode, only show non-demo entries
            _logger.LogDebug(
                "Demo mode OFF: Returning {Count} non-demo entries",
                nonDemoEntries.Count
            );
            return nonDemoEntries;
        }
    }

    /// <inheritdoc />
    public async Task<Entry?> GetEntryByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _entries.GetEntryByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Entry?> CheckForDuplicateEntryAsync(
        string? device,
        string type,
        double? sgv,
        long mills,
        int windowMinutes = 5,
        CancellationToken cancellationToken = default
    )
    {
        return await _entries.CheckForDuplicateEntryAsync(
            device,
            type,
            sgv,
            mills,
            windowMinutes,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var createdEntries = await _entries.CreateEntriesAsync(
            entries,
            cancellationToken
        );

        await _sideEffects.OnCreatedAsync(CollectionName, createdEntries.ToList(), BuildWriteOptions(), cancellationToken);

        return createdEntries;
    }

    /// <inheritdoc />
    public async Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        var updatedEntry = await _entries.UpdateEntryAsync(id, entry, cancellationToken);

        if (updatedEntry != null)
        {
            await _sideEffects.OnUpdatedAsync(CollectionName, updatedEntry, BuildWriteOptionsNoBroadcastDataUpdate(), cancellationToken);
        }

        return updatedEntry;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEntryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        await _sideEffects.BeforeDeleteAsync<Entry>(id, BuildWriteOptions(), cancellationToken);

        // Get the entry before deleting for broadcasting
        var entryToDelete = await _entries.GetEntryByIdAsync(id, cancellationToken);

        var deleted = await _entries.DeleteEntryAsync(id, cancellationToken);

        if (deleted)
        {
            await _sideEffects.OnDeletedAsync(CollectionName, entryToDelete, BuildWriteOptions(), cancellationToken);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteEntriesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // For bulk operations, we'd need to get the entries first if we want to broadcast individual delete events
        // For now, just delete without individual broadcasting (matches current controller behavior)
        var deletedCount = await _entries.BulkDeleteEntriesAsync(
            find ?? "{}",
            cancellationToken
        );

        await _sideEffects.OnBulkDeletedAsync(CollectionName, deletedCount, BuildCacheOnlyOptions(), cancellationToken);

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default)
    {
        var demoSuffix = _demoModeService.IsEnabled ? ":demo" : "";
        var cacheKey = CacheKeyBuilder.BuildCurrentEntriesKey(TenantCacheId) + demoSuffix;
        var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.CurrentEntryExpirationSeconds);

        var cachedEntry = await _cacheService.GetAsync<Entry>(cacheKey, cancellationToken);
        if (cachedEntry != null)
        {
            _logger.LogDebug(
                "Cache HIT for current entry (demoMode: {DemoMode})",
                _demoModeService.IsEnabled
            );
            return cachedEntry;
        }

        _logger.LogDebug(
            "Cache MISS for current entry (demoMode: {DemoMode}), fetching from database",
            _demoModeService.IsEnabled
        );

        // Fetch from legacy entries table and V4 projection sequentially.
        // They share a scoped DbContext which is not thread-safe for concurrent access.
        var findQuery = BuildDemoModeFilterQuery(null);
        var legacyEntry = (await _entries.GetEntriesWithAdvancedFilterAsync(
            type: "sgv",
            count: 1,
            skip: 0,
            findQuery: findQuery,
            cancellationToken: cancellationToken
        )).FirstOrDefault();
        var projectedEntry = await _projectionService.GetLatestProjectedEntryAsync(cancellationToken);

        // Return whichever has the higher Mills timestamp.
        Entry? entry;
        if (legacyEntry == null && projectedEntry == null)
        {
            entry = null;
        }
        else if (legacyEntry == null)
        {
            entry = projectedEntry;
        }
        else if (projectedEntry == null)
        {
            entry = legacyEntry;
        }
        else
        {
            entry = projectedEntry.Mills > legacyEntry.Mills ? projectedEntry : legacyEntry;
        }

        if (entry != null)
        {
            await _cacheService.SetAsync(cacheKey, entry, cacheTtl, cancellationToken);
            _logger.LogDebug("Cached current entry with {TTL}s TTL", cacheTtl.TotalSeconds);
        }

        return entry;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string find,
        int count,
        int skip,
        CancellationToken cancellationToken = default
    )
    {
        // Add demo mode filter to the existing query
        var findQuery = BuildDemoModeFilterQuery(find);
        var entries = await _entries.GetEntriesWithAdvancedFilterAsync(
            null,
            count,
            skip,
            findQuery,
            null,
            false,
            cancellationToken
        );
        return entries;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type,
        int count,
        int skip,
        string? findQuery,
        string? dateString,
        bool reverseResults,
        CancellationToken cancellationToken = default
    )
    {
        // Add demo mode filter to the existing query
        var demoFilteredQuery = BuildDemoModeFilterQuery(findQuery);
        var entries = await _entries.GetEntriesWithAdvancedFilterAsync(
            type,
            count,
            skip,
            demoFilteredQuery,
            dateString,
            reverseResults,
            cancellationToken
        );
        return entries;
    }
}

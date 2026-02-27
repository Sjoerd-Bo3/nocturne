using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Constants;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for treatment operations with WebSocket broadcasting
/// </summary>
public class TreatmentService : ITreatmentService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ISignalRBroadcastService _broadcastService;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly IDemoModeService _demoModeService;
    private readonly IStateSpanService _stateSpanService;
    private readonly ITreatmentDecomposer _treatmentDecomposer;
    private readonly IV4ToLegacyProjectionService _projectionService;
    private readonly ITempBasalRepository _tempBasalRepository;
    private readonly ILogger<TreatmentService> _logger;
    private const string CollectionName = "treatments";
    private const string DefaultTenantId = "default"; // TODO: Replace with actual tenant context

    public TreatmentService(
        IPostgreSqlService postgreSqlService,
        ISignalRBroadcastService broadcastService,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        IDemoModeService demoModeService,
        IStateSpanService stateSpanService,
        ITreatmentDecomposer treatmentDecomposer,
        IV4ToLegacyProjectionService projectionService,
        ITempBasalRepository tempBasalRepository,
        ILogger<TreatmentService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _broadcastService = broadcastService;
        _cacheService = cacheService;
        _cacheConfig = cacheConfig.Value;
        _demoModeService = demoModeService;
        _stateSpanService = stateSpanService;
        _treatmentDecomposer = treatmentDecomposer;
        _projectionService = projectionService;
        _tempBasalRepository = tempBasalRepository;
        _logger = logger;
    }

    // BuildDemoModeFilterQuery removed - relying on database isolation

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        var actualCount = count ?? 10;
        var actualSkip = skip ?? 0;

        // Use find query directly (no application-level demo filter needed due to DB isolation)
        var findQuery = find;

        // If find query is provided, use advanced filtering (no caching for filtered queries)
        if (!string.IsNullOrEmpty(find))
        {
            _logger.LogDebug(
                "Using advanced filter for treatments with findQuery: {FindQuery}, count: {Count}, skip: {Skip}, demoMode: {DemoMode}",
                findQuery,
                actualCount,
                actualSkip,
                _demoModeService.IsEnabled
            );
            var treatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
                count: actualCount,
                skip: 0, // We'll handle skip in the merge
                findQuery: findQuery,
                reverseResults: false,
                cancellationToken: cancellationToken
            );
            return await MergeWithTempBasalsAsync(
                treatments,
                findQuery,
                actualCount,
                actualSkip,
                cancellationToken
            );
        }

        // Cache recent treatments for common queries (skip = 0 and common counts)
        // Include demo mode in cache key to avoid mixing demo/non-demo data
        if (actualSkip == 0 && IsCommonTreatmentCount(actualCount))
        {
            // Determine time range based on common patterns (default to 24 hours for treatments)
            var hours = DetermineTimeRangeHours(actualCount);
            var demoSuffix = _demoModeService.IsEnabled ? ":demo" : "";
            var cacheKey =
                CacheKeyBuilder.BuildRecentTreatmentsKey(DefaultTenantId, hours, actualCount)
                + demoSuffix;
            var cacheTtl = TimeSpan.FromSeconds(
                CacheConstants.Defaults.RecentTreatmentsExpirationSeconds
            );

            var cachedTreatments = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent treatments (count: {Count}, hours: {Hours}, demoMode: {DemoMode}), fetching from database with filter: {Filter}",
                        actualCount,
                        hours,
                        _demoModeService.IsEnabled,
                        findQuery
                    );
                    var treatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
                        count: actualCount,
                        skip: 0, // We'll handle skip in the merge
                        findQuery: findQuery,
                        reverseResults: false,
                        cancellationToken: cancellationToken
                    );
                    return treatments.ToList();
                },
                cacheTtl,
                cancellationToken
            );
            return await MergeWithTempBasalsAsync(
                cachedTreatments,
                findQuery,
                actualCount,
                actualSkip,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        var allTreatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
            count: actualCount,
            skip: 0, // We'll handle skip in the merge
            findQuery: findQuery,
            reverseResults: false,
            cancellationToken: cancellationToken
        );
        return await MergeWithTempBasalsAsync(
            allTreatments,
            findQuery,
            actualCount,
            actualSkip,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Use null find query (no application-level demo filter needed due to DB isolation)
        string? findQuery = null;

        // Cache recent treatments for common queries (skip = 0 and common counts)
        // Include demo mode in cache key to avoid mixing demo/non-demo data
        if (skip == 0 && IsCommonTreatmentCount(count))
        {
            // Determine time range based on common patterns (default to 24 hours for treatments)
            var hours = DetermineTimeRangeHours(count);
            var demoSuffix = _demoModeService.IsEnabled ? ":demo" : "";
            var cacheKey =
                CacheKeyBuilder.BuildRecentTreatmentsKey(DefaultTenantId, hours, count)
                + demoSuffix;
            var cacheTtl = TimeSpan.FromSeconds(
                CacheConstants.Defaults.RecentTreatmentsExpirationSeconds
            );

            var cachedTreatments = await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent treatments (count: {Count}, hours: {Hours}, demoMode: {DemoMode}), fetching from database with filter: {Filter}",
                        count,
                        hours,
                        _demoModeService.IsEnabled,
                        findQuery
                    );
                    var treatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
                        count: count,
                        skip: 0, // We'll handle skip in the merge
                        findQuery: findQuery,
                        reverseResults: false,
                        cancellationToken: cancellationToken
                    );
                    return treatments.ToList();
                },
                cacheTtl,
                cancellationToken
            );
            return await MergeWithTempBasalsAsync(
                cachedTreatments,
                findQuery,
                count,
                skip,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        var allTreatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
            count: count,
            skip: 0, // We'll handle skip in the merge
            findQuery: findQuery,
            reverseResults: false,
            cancellationToken: cancellationToken
        );
        return await MergeWithTempBasalsAsync(
            allTreatments,
            findQuery,
            count,
            skip,
            cancellationToken
        );
    }

    /// <summary>
    /// Determines if the treatment count is common enough to cache
    /// </summary>
    /// <param name="count">The count to check</param>
    /// <returns>True if the count is common (10, 50, 100), false otherwise</returns>
    private static bool IsCommonTreatmentCount(int count)
    {
        return count is 10 or 50 or 100;
    }

    /// <summary>
    /// Determines the appropriate time range hours based on treatment count
    /// </summary>
    /// <param name="count">The treatment count</param>
    /// <returns>Time range in hours (12, 24, or 48)</returns>
    private static int DetermineTimeRangeHours(int count)
    {
        return count switch
        {
            <= 10 => 12, // 12 hours for small counts
            <= 50 => 24, // 24 hours for medium counts
            _ => 48, // 48 hours for large counts
        };
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
    /// Merges regular treatments with V4 TempBasal records and V4-projected treatments
    /// for V1-V3 API backwards compatibility.
    /// </summary>
    private async Task<IEnumerable<Treatment>> MergeWithTempBasalsAsync(
        IEnumerable<Treatment> treatments,
        string? findQuery,
        int count,
        int skip,
        CancellationToken cancellationToken
    )
    {
        var (fromMills, toMills) = ParseTimeRangeFromFind(findQuery);

        // Get temp basals from V4 TempBasal table
        var tempBasalTask = _tempBasalRepository.GetAsync(
            from: fromMills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(fromMills.Value).UtcDateTime : null,
            to: toMills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(toMills.Value).UtcDateTime : null,
            device: null,
            source: null,
            limit: count,
            offset: 0, // We'll handle skip in the merge
            descending: true,
            ct: cancellationToken
        );

        // Get V4-projected treatments (native V4 connector writes)
        var projectedTask = _projectionService.GetProjectedTreatmentsAsync(
            fromMills,
            toMills,
            count,
            cancellationToken
        );

        await Task.WhenAll(tempBasalTask, projectedTask);

        var tempBasals = await tempBasalTask;
        var tempBasalTreatments = TempBasalToTreatmentMapper.ToTreatments(tempBasals).ToList();
        var projectedTreatments = await projectedTask;

        // Build a set of legacy treatment IDs for dedup; V4 projection only returns records
        // with LegacyId == null, so there will be no Id overlap with legacy treatments.
        // Dedup by Mills to guard against timestamp collisions.
        var legacyList = treatments.ToList();
        var legacyMillsSet = legacyList.Select(t => t.Mills).ToHashSet();
        var basalMillsSet = tempBasalTreatments.Select(t => t.Mills).ToHashSet();

        var filteredProjected = projectedTreatments
            .Where(p => !legacyMillsSet.Contains(p.Mills) && !basalMillsSet.Contains(p.Mills));

        // Merge all sources and sort
        var allTreatments = legacyList
            .Concat(tempBasalTreatments)
            .Concat(filteredProjected)
            .OrderByDescending(t => t.Mills)
            .Skip(skip)
            .Take(count)
            .ToList();

        return allTreatments;
    }

    /// <inheritdoc />
    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // First check the treatments table
        var treatment = await _postgreSqlService.GetTreatmentByIdAsync(id, cancellationToken);
        if (treatment != null)
            return treatment;

        // Try V4 TempBasal table by legacy ID first, then by GUID
        var tempBasal = await _tempBasalRepository.GetByLegacyIdAsync(id, cancellationToken);
        if (tempBasal == null && Guid.TryParse(id, out var guid))
            tempBasal = await _tempBasalRepository.GetByIdAsync(guid, cancellationToken);
        if (tempBasal != null)
            return TempBasalToTreatmentMapper.ToTreatment(tempBasal);

        return null;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count,
        int skip,
        string? findQuery,
        bool reverseResults,
        CancellationToken cancellationToken = default
    )
    {
        var treatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
            count: count,
            skip: 0,
            findQuery: findQuery,
            reverseResults: false,
            cancellationToken: cancellationToken
        );

        var merged = await MergeWithTempBasalsAsync(
            treatments,
            findQuery,
            count,
            skip,
            cancellationToken
        );

        return reverseResults
            ? merged.OrderBy(t => t.Mills)
            : merged.OrderByDescending(t => t.Mills);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    )
    {
        var treatments = await _postgreSqlService.GetTreatmentsModifiedSinceAsync(
            lastModifiedMills,
            limit,
            cancellationToken
        );

        // Get temp basals from V4 TempBasal table (primary path)
        var tempBasals = await _tempBasalRepository.GetAsync(
            from: DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMills).UtcDateTime,
            to: (DateTime?)null,
            device: null,
            source: null,
            limit: limit,
            offset: 0,
            descending: true,
            ct: cancellationToken
        );
        var tempBasalTreatments = TempBasalToTreatmentMapper.ToTreatments(tempBasals).ToList();

        return treatments
            .Concat(tempBasalTreatments)
            .OrderBy(t => t.SrvModified ?? t.Mills)
            .Take(limit);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var treatmentList = treatments.ToList();
        var regularTreatments = new List<Treatment>();
        var stateSpanTreatments = new List<Treatment>();
        var algorithmBolusTreatments = new List<Treatment>();

        // Separate treatments into buckets:
        // 1. Temp basals → skip legacy table, go to decomposer (StateSpan/TempBasal path)
        // 2. Algorithm boluses / SMBs (IsBasalInsulin == true with insulin) → skip legacy table, go to decomposer
        // 3. Regular → write to legacy table, then decompose
        foreach (var treatment in treatmentList)
        {
            if (TreatmentStateSpanMapper.IsTempBasalTreatment(treatment))
            {
                stateSpanTreatments.Add(treatment);
            }
            else if (treatment.IsBasalInsulin == true && treatment.Insulin > 0)
            {
                algorithmBolusTreatments.Add(treatment);
            }
            else
            {
                regularTreatments.Add(treatment);
            }
        }

        var results = new List<Treatment>();

        // Process temp basal treatments through the decomposer (written to V4 TempBasal table, not legacy table)
        foreach (var tempBasalTreatment in stateSpanTreatments)
        {
            try
            {
                var decompositionResult = await _treatmentDecomposer.DecomposeAsync(
                    tempBasalTreatment,
                    cancellationToken
                );

                // Extract the created TempBasal from the decomposition result to build the treatment response
                var createdTempBasal = decompositionResult.CreatedRecords
                    .OfType<Core.Models.V4.TempBasal>()
                    .FirstOrDefault();
                var createdTreatment = createdTempBasal != null
                    ? TempBasalToTreatmentMapper.ToTreatment(createdTempBasal)
                    : null;

                if (createdTreatment != null)
                {
                    results.Add(createdTreatment);

                    try
                    {
                        await _broadcastService.BroadcastStorageCreateAsync(
                            CollectionName,
                            new { colName = CollectionName, doc = createdTreatment }
                        );
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to broadcast storage create event for temp basal {TreatmentId}",
                            createdTreatment.Id
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to decompose temp basal treatment {Id}",
                    tempBasalTreatment.Id
                );
            }
        }

        // Process algorithm bolus (SMB) treatments through the decomposer (not written to legacy table)
        foreach (var algorithmBolus in algorithmBolusTreatments)
        {
            try
            {
                var decompositionResult = await _treatmentDecomposer.DecomposeAsync(
                    algorithmBolus,
                    cancellationToken
                );

                // For algorithm boluses the decomposer writes to the Bolus repo with Kind=Algorithm;
                // return the original treatment shape so callers see what was accepted.
                results.Add(algorithmBolus);

                try
                {
                    await _broadcastService.BroadcastStorageCreateAsync(
                        CollectionName,
                        new { colName = CollectionName, doc = algorithmBolus }
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to broadcast storage create event for algorithm bolus {TreatmentId}",
                        algorithmBolus.Id
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to decompose algorithm bolus treatment {Id}",
                    algorithmBolus.Id
                );
            }
        }

        // Process regular treatments through existing path
        if (regularTreatments.Count > 0)
        {
            var createdTreatments = await _postgreSqlService.CreateTreatmentsAsync(
                regularTreatments,
                cancellationToken
            );

            // Invalidate all recent treatments caches since new treatments were created
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogInformation(
                    "Cache INVALIDATION: recent treatments pattern '{Pattern}' after creating {Count} treatments",
                    recentTreatmentsPattern,
                    createdTreatments.Count()
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }

            // Broadcast create events for each treatment (replaces legacy ctx.bus.emit('storage-socket-create'))
            foreach (var treatment in createdTreatments)
            {
                try
                {
                    await _broadcastService.BroadcastStorageCreateAsync(
                        CollectionName,
                        new { colName = CollectionName, doc = treatment }
                    );
                    _logger.LogDebug(
                        "Broadcasted storage create event for treatment {TreatmentId}",
                        treatment.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to broadcast storage create event for treatment {TreatmentId}",
                        treatment.Id
                    );
                }
            }

            // Decompose each created treatment into v4 tables
            foreach (var treatment in createdTreatments)
            {
                try
                {
                    await _treatmentDecomposer.DecomposeAsync(treatment, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to decompose treatment {TreatmentId} into v4 tables",
                        treatment.Id
                    );
                }
            }

            results.AddRange(createdTreatments);
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        // Check if this is a temp basal in the V4 TempBasal table
        var existingTempBasal = await _tempBasalRepository.GetByLegacyIdAsync(id, cancellationToken);
        if (existingTempBasal == null && Guid.TryParse(id, out var tempBasalGuid))
            existingTempBasal = await _tempBasalRepository.GetByIdAsync(tempBasalGuid, cancellationToken);

        if (existingTempBasal != null)
        {
            try
            {
                treatment.Id = id;
                await _treatmentDecomposer.DecomposeAsync(treatment, cancellationToken);

                // Re-read after decompose to get updated values
                var refreshed = await _tempBasalRepository.GetByIdAsync(existingTempBasal.Id, cancellationToken);
                var updatedTreatment = refreshed != null
                    ? TempBasalToTreatmentMapper.ToTreatment(refreshed)
                    : TempBasalToTreatmentMapper.ToTreatment(existingTempBasal);

                try
                {
                    await _broadcastService.BroadcastStorageUpdateAsync(
                        CollectionName,
                        new { colName = CollectionName, doc = updatedTreatment }
                    );
                    _logger.LogDebug(
                        "Broadcasted storage update event for temp basal treatment {TreatmentId}",
                        updatedTreatment.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to broadcast storage update event for temp basal {TreatmentId}",
                        updatedTreatment.Id
                    );
                }

                return updatedTreatment;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to update TempBasal-backed treatment {TreatmentId}",
                    id
                );
                return null;
            }
        }

        // Fall back to regular treatment update
        var regularUpdatedTreatment = await _postgreSqlService.UpdateTreatmentAsync(
            id,
            treatment,
            cancellationToken
        );

        if (regularUpdatedTreatment != null)
        {
            // Invalidate all recent treatments caches since a treatment was updated
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogInformation(
                    "Cache INVALIDATION: recent treatments pattern '{Pattern}' after updating treatment {TreatmentId}",
                    recentTreatmentsPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageUpdateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = regularUpdatedTreatment }
                );
                _logger.LogDebug(
                    "Broadcasted storage update event for treatment {TreatmentId}",
                    regularUpdatedTreatment.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage update event for treatment {TreatmentId}",
                    regularUpdatedTreatment.Id
                );
            }

            // Re-decompose the updated treatment to keep v4 tables in sync
            try
            {
                await _treatmentDecomposer.DecomposeAsync(
                    regularUpdatedTreatment,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to re-decompose updated treatment {TreatmentId} into v4 tables",
                    regularUpdatedTreatment.Id
                );
            }
        }

        return regularUpdatedTreatment;
    }

    /// <inheritdoc />
    public async Task<Treatment?> PatchTreatmentAsync(
        string id,
        JsonElement patchData,
        CancellationToken cancellationToken = default
    )
    {
        // First try the treatments table
        var patched = await _postgreSqlService.PatchTreatmentAsync(
            id,
            patchData,
            cancellationToken
        );
        if (patched != null)
        {
            await BroadcastAndInvalidateAsync(patched, cancellationToken);
            return patched;
        }

        return null;
    }

    private static void ApplyJsonMergePatch(Treatment treatment, JsonElement patchData)
    {
        foreach (var prop in patchData.EnumerateObject())
        {
            var isNull = prop.Value.ValueKind == JsonValueKind.Null;
            switch (prop.Name)
            {
                case "duration":
                    treatment.Duration = isNull ? null : prop.Value.GetDouble();
                    break;
                case "durationInMilliseconds":
                    treatment.DurationInMilliseconds = isNull ? null : prop.Value.GetInt64();
                    if (!isNull)
                        treatment.Duration = prop.Value.GetInt64() / 60000.0;
                    break;
                case "rate":
                    treatment.Rate = isNull ? null : prop.Value.GetDouble();
                    break;
                case "absolute":
                    treatment.Absolute = isNull ? null : prop.Value.GetDouble();
                    break;
                case "percent":
                    treatment.Percent = isNull ? null : prop.Value.GetDouble();
                    break;
                case "enteredBy":
                    treatment.EnteredBy = isNull ? null : prop.Value.GetString();
                    break;
                case "endId":
                    treatment.EndId = isNull ? null : prop.Value.GetInt64();
                    break;
                case "isValid":
                    treatment.IsValid = isNull ? null : prop.Value.GetBoolean();
                    break;
                case "isReadOnly":
                    treatment.IsReadOnly = isNull ? null : prop.Value.GetBoolean();
                    break;
                case "pumpId":
                    treatment.PumpId = isNull ? null : prop.Value.GetInt64();
                    break;
                case "pumpType":
                    treatment.PumpType = isNull ? null : prop.Value.GetString();
                    break;
                case "pumpSerial":
                    treatment.PumpSerial = isNull ? null : prop.Value.GetString();
                    break;
                case "insulin":
                    treatment.Insulin = isNull ? null : prop.Value.GetDouble();
                    break;
                case "isBasalInsulin":
                    treatment.IsBasalInsulin = isNull ? null : prop.Value.GetBoolean();
                    break;
            }
        }
    }

    private async Task BroadcastAndInvalidateAsync(
        Treatment treatment,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _broadcastService.BroadcastStorageUpdateAsync(
                CollectionName,
                new { colName = CollectionName, doc = treatment }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast update for treatment {TreatmentId}",
                treatment.Id
            );
        }

        try
        {
            var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                DefaultTenantId
            );
            await _cacheService.RemoveByPatternAsync(recentTreatmentsPattern, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate treatment caches after patch");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTreatmentAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Delete corresponding v4 records by LegacyId
        try
        {
            await _treatmentDecomposer.DeleteByLegacyIdAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete v4 records for legacy treatment {TreatmentId}", id);
        }

        // Check if this is a temp basal in the V4 TempBasal table
        var existingTempBasal = await _tempBasalRepository.GetByLegacyIdAsync(id, cancellationToken);
        if (existingTempBasal == null && Guid.TryParse(id, out var tempBasalDeleteGuid))
            existingTempBasal = await _tempBasalRepository.GetByIdAsync(tempBasalDeleteGuid, cancellationToken);

        if (existingTempBasal != null)
        {
            var treatmentForBroadcast = TempBasalToTreatmentMapper.ToTreatment(existingTempBasal);

            try
            {
                await _tempBasalRepository.DeleteAsync(existingTempBasal.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete TempBasal record {TempBasalId}",
                    existingTempBasal.Id
                );
                return false;
            }

            try
            {
                await _broadcastService.BroadcastStorageDeleteAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = treatmentForBroadcast }
                );
                _logger.LogDebug(
                    "Broadcasted storage delete event for temp basal treatment {TreatmentId}",
                    treatmentForBroadcast.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage delete event for temp basal {TreatmentId}",
                    treatmentForBroadcast.Id
                );
            }

            return true;
        }

        // Fall back to regular treatment delete
        // Get the treatment before deleting for broadcasting
        var treatmentToDelete = await _postgreSqlService.GetTreatmentByIdAsync(
            id,
            cancellationToken
        );

        var regularDeleted = await _postgreSqlService.DeleteTreatmentAsync(id, cancellationToken);

        if (regularDeleted && treatmentToDelete != null)
        {
            // Invalidate all recent treatments caches since a treatment was deleted
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogInformation(
                    "Cache INVALIDATION: recent treatments pattern '{Pattern}' after deleting treatment {TreatmentId}",
                    recentTreatmentsPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageDeleteAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = treatmentToDelete }
                );
                _logger.LogDebug(
                    "Broadcasted storage delete event for treatment {TreatmentId}",
                    treatmentToDelete.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage delete event for treatment {TreatmentId}",
                    treatmentToDelete.Id
                );
            }
        }

        return regularDeleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteTreatmentsAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // For bulk operations, we'd need to get the treatments first if we want to broadcast individual delete events
        // For now, just delete without individual broadcasting (matches current controller behavior)
        var deletedCount = await _postgreSqlService.BulkDeleteTreatmentsAsync(
            find ?? "{}",
            cancellationToken
        );

        if (deletedCount > 0)
        {
            // Invalidate all recent treatments caches since treatments were deleted
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogDebug(
                    "Invalidated recent treatments pattern '{Pattern}' after bulk deleting {Count} treatments",
                    recentTreatmentsPattern,
                    deletedCount
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }
        }

        return deletedCount;
    }
}

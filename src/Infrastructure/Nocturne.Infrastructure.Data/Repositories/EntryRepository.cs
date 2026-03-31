using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;
using Npgsql;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for Entry operations
/// </summary>
public class EntryRepository : IEntryRepository
{
    private readonly NocturneDbContext _context;
    private readonly IQueryParser _queryParser;
    private readonly IDeduplicationService _deduplicationService;
    private readonly ILogger<EntryRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the EntryRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="queryParser">MongoDB query parser for advanced filtering</param>
    /// <param name="deduplicationService">Service for deduplicating records</param>
    /// <param name="logger">Logger instance</param>
    public EntryRepository(
        NocturneDbContext context,
        IQueryParser queryParser,
        IDeduplicationService deduplicationService,
        ILogger<EntryRepository> logger)
    {
        _context = context;
        _queryParser = queryParser;
        _deduplicationService = deduplicationService;
        _logger = logger;
    }

    /// <summary>
    /// Get entries with optional filtering and pagination
    /// </summary>
    /// <param name="type">Optional entry type filter.</param>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <param name="skip">The number of entries to skip.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of entries.</returns>
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Entries.AsQueryable();

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(e => e.Type == type);
        }

        // Order by Mills descending (most recent first), then apply pagination
        var entities = await query
            .OrderByDescending(e => e.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(EntryMapper.ToDomainModel);
    }

    /// <summary>
    /// Get the most recent entry
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current entry, or null if none exists.</returns>
    public async Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default)
    {
        var entity = await _context
            .Entries.OrderByDescending(e => e.Mills)
            .FirstOrDefaultAsync(cancellationToken);

        return entity != null ? EntryMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get a specific entry by ID
    /// </summary>
    /// <param name="id">The unique identifier (GUID or legacy string ID).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The entry, or null if not found.</returns>
    public async Task<Entry?> GetEntryByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Entries.FirstOrDefaultAsync(
            e => e.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Entries.FirstOrDefaultAsync(
                e => e.Id == guidId,
                cancellationToken
            );
        }

        return entity != null ? EntryMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Create new entries, skipping duplicates and linking to canonical groups
    /// </summary>
    /// <param name="entries">The collection of entries to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of created entries.</returns>
    public async Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var entities = entries.Select(EntryMapper.ToEntity).ToList();

        if (entities.Count == 0)
        {
            return Enumerable.Empty<Entry>();
        }

        // Deduplicate within the batch itself (e.g. connector returns overlapping data)
        entities = entities
            .GroupBy(e => e.Id)
            .Select(g => g.First())
            .ToList();

        // Get the IDs of the entities we're trying to insert
        var entityIds = entities.Select(e => e.Id).ToHashSet();

        // Check which IDs already exist in the database
        var existingIds = await _context
            .Entries.Where(e => entityIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToHashSetAsync(cancellationToken);

        // Filter out entities that already exist
        var newEntities = entities.Where(e => !existingIds.Contains(e.Id)).ToList();

        if (newEntities.Count == 0)
        {
            // All entries already exist, return empty
            return Enumerable.Empty<Entry>();
        }

        const int batchSize = 500;
        var savedEntities = new List<EntryEntity>();

        foreach (var batch in newEntities.Chunk(batchSize))
        {
            var batchList = batch.ToList();
            _context.Entries.AddRange(batchList);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                savedEntities.AddRange(batchList);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                _logger.LogWarning("Duplicate key conflict during entry insert batch, retrying with fresh deduplication");

                foreach (var entity in batchList)
                {
                    _context.Entry(entity).State = EntityState.Detached;
                }

                var batchIds = batchList.Select(e => e.Id).ToHashSet();
                var nowExistingIds = await _context
                    .Entries.Where(e => batchIds.Contains(e.Id))
                    .Select(e => e.Id)
                    .ToHashSetAsync(cancellationToken);

                var retryEntities = batchList.Where(e => !nowExistingIds.Contains(e.Id)).ToList();

                if (retryEntities.Count > 0)
                {
                    _context.Entries.AddRange(retryEntities);
                    await _context.SaveChangesAsync(cancellationToken);
                    savedEntities.AddRange(retryEntities);
                }
            }
            _context.ChangeTracker.Clear();
        }

        newEntities = savedEntities;

        // Link new entries to canonical groups for deduplication
        foreach (var entity in newEntities)
        {
            try
            {
                var criteria = new MatchCriteria
                {
                    GlucoseValue = entity.Sgv ?? entity.Mgdl,
                    GlucoseTolerance = 1.0 // 1 mg/dL tolerance for glucose matching
                };

                var canonicalId = await _deduplicationService.GetOrCreateCanonicalIdAsync(
                    RecordType.Entry,
                    entity.Mills,
                    criteria,
                    cancellationToken);

                await _deduplicationService.LinkRecordAsync(
                    canonicalId,
                    RecordType.Entry,
                    entity.Id,
                    entity.Mills,
                    entity.DataSource ?? "unknown",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't fail the insert if deduplication fails
                _logger.LogWarning(ex, "Failed to deduplicate entry {EntryId}", entity.Id);
            }
        }

        return newEntities.Select(EntryMapper.ToDomainModel);
    }

    /// <summary>
    /// Update an existing entry
    /// </summary>
    /// <param name="id">The unique identifier of the entry to update.</param>
    /// <param name="entry">The updated entry data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated entry, or null if not found.</returns>
    public async Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Entries.FirstOrDefaultAsync(
            e => e.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Entries.FirstOrDefaultAsync(
                e => e.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
            return null;

        EntryMapper.UpdateEntity(entity, entry);
        await _context.SaveChangesAsync(cancellationToken);

        return EntryMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete an entry
    /// </summary>
    /// <param name="id">The unique identifier of the entry to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the entry was deleted, otherwise false.</returns>
    public async Task<bool> DeleteEntryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Entries.FirstOrDefaultAsync(
            e => e.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Entries.FirstOrDefaultAsync(
                e => e.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
            return false;

        entity.DeletedAt = DateTime.UtcNow;
        entity.SysUpdatedAt = DateTime.UtcNow;
        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    /// <summary>
    /// Delete multiple entries with optional filtering
    /// </summary>
    /// <param name="type">Optional entry type filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted records.</returns>
    public async Task<long> DeleteEntriesAsync(
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Entries.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(e => e.Type == type);
        }

        var now = DateTime.UtcNow;
        var deletedCount = await query.ExecuteUpdateAsync(
            s => s
                .SetProperty(e => e.DeletedAt, now)
                .SetProperty(e => e.SysUpdatedAt, now),
            cancellationToken
        );
        return deletedCount;
    }

    /// <summary>
    /// Delete all entries with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source to filter by (e.g., "demo-service")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of entries deleted</returns>
    public async Task<long> DeleteByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        var deletedCount = await _context
            .Entries.Where(e => e.DataSource == dataSource)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(e => e.DeletedAt, now)
                    .SetProperty(e => e.SysUpdatedAt, now),
                cancellationToken
            );
        return deletedCount;
    }

    /// <summary>
    /// Get entries with advanced filtering (simplified version for now)
    /// </summary>
    /// <remarks>
    /// TODO: Complex MongoDB-style query parsing is not yet implemented.
    /// Currently supports basic type and date filtering.
    /// </remarks>
    /// <param name="type">Optional entry type filter.</param>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <param name="skip">The number of entries to skip.</param>
    /// <param name="findQuery">Optional MongoDB-style find query string.</param>
    /// <param name="dateString">Optional date string filter.</param>
    /// <param name="reverseResults">Whether to reverse the order of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of entries.</returns>
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? dateString = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Entries.AsQueryable();

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(e => e.Type == type);
        }

        // Apply date filter if specified
        if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var filterDate))
        {
            var filterMills = ((DateTimeOffset)filterDate).ToUnixTimeMilliseconds();
            query = query.Where(e => e.Mills >= filterMills);
        }

        // Apply advanced MongoDB-style query filtering
        if (!string.IsNullOrEmpty(findQuery))
        {
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
            };

            query = await _queryParser.ApplyQueryAsync(
                query,
                findQuery,
                options,
                cancellationToken
            );
        }
        else
        {
            // Apply default date filter when no find query is specified
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
            };

            query = _queryParser.ApplyDefaultDateFilter(query, findQuery, dateString, options);
        }

        // Apply ordering
        if (reverseResults)
        {
            query = query.OrderBy(e => e.Mills);
        }
        else
        {
            query = query.OrderByDescending(e => e.Mills);
        }

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(EntryMapper.ToDomainModel);
    }

    /// <summary>
    /// Count entries with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional MongoDB-style find query string.</param>
    /// <param name="type">Optional entry type filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of matching entries.</returns>
    public async Task<long> CountEntriesAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Entries.AsQueryable();

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(e => e.Type == type);
        }

        // Apply advanced MongoDB-style query filtering
        if (!string.IsNullOrEmpty(findQuery))
        {
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
                DisableDefaultDateFilter = true, // Count queries don't need auto date filtering
            };

            query = await _queryParser.ApplyQueryAsync(
                query,
                findQuery,
                options,
                cancellationToken
            );
        }

        return await query.CountAsync(cancellationToken);
    }

    /// <summary>
    /// Create a single entry
    /// </summary>
    /// <param name="entry">The entry to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created entry, or null if creation failed.</returns>
    public async Task<Entry?> CreateEntryAsync(
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        var createdEntries = await CreateEntriesAsync([entry], cancellationToken);
        return createdEntries.FirstOrDefault();
    }

    /// <summary>
    /// Get the latest entry timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest entry timestamp, or null if none found.</returns>
    public async Task<DateTime?> GetLatestEntryTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var latestEntry = await _context
            .Entries.Where(e => e.DataSource == dataSource)
            .OrderByDescending(e => e.Mills)
            .Select(e => new { e.Mills })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestEntry == null)
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds(latestEntry.Mills).UtcDateTime;
    }

    /// <summary>
    /// Get the oldest entry timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The oldest entry timestamp, or null if none found.</returns>
    public async Task<DateTime?> GetOldestEntryTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var oldestEntry = await _context
            .Entries.Where(e => e.DataSource == dataSource)
            .OrderBy(e => e.Mills)
            .Select(e => new { e.Mills })
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestEntry == null)
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds(oldestEntry.Mills).UtcDateTime;
    }

    /// <summary>
    /// Check for duplicate entries in the database within a time window
    /// </summary>
    /// <param name="device">The device name.</param>
    /// <param name="type">The entry type.</param>
    /// <param name="sgv">The glucose value.</param>
    /// <param name="mills">The timestamp in unix milliseconds.</param>
    /// <param name="windowMinutes">The search window in minutes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The duplicate entry if found, otherwise null.</returns>
    public async Task<Entry?> CheckForDuplicateEntryAsync(
        string? device,
        string type,
        double? sgv,
        long mills,
        int windowMinutes = 5,
        CancellationToken cancellationToken = default
    )
    {
        // Calculate time window in milliseconds
        var windowMs = windowMinutes * 60 * 1000L;
        var windowStart = mills - windowMs;
        var windowEnd = mills + windowMs;

        // Query database for duplicate using composite index
        var duplicate = await _context
            .Entries.Where(e => e.Device == device)
            .Where(e => e.Type == type)
            .Where(e => e.Sgv == sgv)
            .Where(e => e.Mills >= windowStart && e.Mills <= windowEnd)
            .OrderBy(e => e.Mills) // Use index order
            .Select(e => new Entry
            {
                Id = e.Id.ToString(),
                Mills = e.Mills,
                DateString = e.DateString,
                Mgdl = e.Mgdl,
                Mmol = e.Mmol,
                Sgv = e.Sgv,
                Direction = e.Direction,
                Type = e.Type,
                Device = e.Device,
                Notes = e.Notes,
                Delta = e.Delta,
                SysTime = e.SysTime,
                UtcOffset = e.UtcOffset,
                Noise = e.Noise,
                Filtered = e.Filtered,
                Unfiltered = e.Unfiltered,
                Rssi = e.Rssi,
                Slope = e.Slope,
                Intercept = e.Intercept,
                Scale = e.Scale,
                CreatedAt = e.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return duplicate;
    }

    /// <summary>
    /// Delete all entries with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted records.</returns>
    public async Task<long> DeleteEntriesByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        return await DeleteByDataSourceAsync(dataSource, cancellationToken);
    }

    /// <summary>
    /// Bulk delete entries using query filters
    /// </summary>
    /// <param name="findQuery">The filter criteria for deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted records.</returns>
    public async Task<long> BulkDeleteEntriesAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        // For now, treat findQuery as a type filter - this could be expanded later
        return await DeleteEntriesAsync(findQuery, cancellationToken);
    }

    /// <summary>
    /// Get entries modified since a given timestamp (for incremental sync)
    /// </summary>
    /// <param name="lastModifiedMills">The timestamp in unix milliseconds.</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of entries modified since the timestamp.</returns>
    public async Task<IEnumerable<Entry>> GetEntriesModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    )
    {
        var threshold = DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMills).UtcDateTime;
        var entities = await _context
            .Entries.IgnoreQueryFilters()
            .Where(e => e.TenantId == _context.TenantId)
            .Where(e => e.SysUpdatedAt >= threshold)
            .OrderBy(e => e.SysUpdatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return entities.Select(EntryMapper.ToDomainModel);
    }

    /// <summary>
    /// Get statistics for entries from a specific data source
    /// </summary>
    /// <param name="dataSource">The data source filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Statistics for the specified data source.</returns>
    public async Task<DataSourceStats> GetEntryStatsBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTimeOffset.UtcNow;
        var oneDayAgo = now.AddHours(-24).ToUnixTimeMilliseconds();
        var oneDayAgoDate = now.AddHours(-24).UtcDateTime;

        // Query entry stats
        var entryStats = await _context
            .Entries.Where(e => e.DataSource == dataSource || e.Device == dataSource)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalEntries = g.LongCount(),
                EntriesLast24Hours = g.Count(e => e.Mills >= oneDayAgo),
                LastEntryMills = g.Max(e => (long?)e.Mills),
                FirstEntryMills = g.Min(e => (long?)e.Mills),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Query treatment stats
        var treatmentStats = await _context
            .Treatments.Where(t => t.DataSource == dataSource || t.EnteredBy == dataSource)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalTreatments = g.LongCount(),
                TreatmentsLast24Hours = g.Count(t => t.Mills >= oneDayAgo),
                LastTreatmentMills = g.Max(t => (long?)t.Mills),
                FirstTreatmentMills = g.Min(t => (long?)t.Mills),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Query state span stats
        var stateSpanStats = await _context
            .StateSpans.Where(s => s.Source == dataSource)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalStateSpans = g.LongCount(),
                StateSpansLast24Hours = g.Count(s => s.StartTimestamp >= oneDayAgoDate),
                LastStateSpanTime = g.Max(s => (DateTime?)s.StartTimestamp),
                FirstStateSpanTime = g.Min(s => (DateTime?)s.StartTimestamp),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Query V4 table counts for per-type breakdown
        var sensorGlucoseTotal = await _context
            .SensorGlucose.Where(sg => sg.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var sensorGlucose24h = sensorGlucoseTotal > 0
            ? await _context
                .SensorGlucose.Where(sg => sg.DataSource == dataSource && sg.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var meterGlucoseTotal = await _context
            .MeterGlucose.Where(mg => mg.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var meterGlucose24h = meterGlucoseTotal > 0
            ? await _context
                .MeterGlucose.Where(mg => mg.DataSource == dataSource && mg.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var bolusesTotal = await _context
            .Boluses.Where(b => b.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var boluses24h = bolusesTotal > 0
            ? await _context
                .Boluses.Where(b => b.DataSource == dataSource && b.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var carbIntakesTotal = await _context
            .CarbIntakes.Where(c => c.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var carbIntakes24h = carbIntakesTotal > 0
            ? await _context
                .CarbIntakes.Where(c => c.DataSource == dataSource && c.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var bolusCalcsTotal = await _context
            .BolusCalculations.Where(bc => bc.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var bolusCalcs24h = bolusCalcsTotal > 0
            ? await _context
                .BolusCalculations.Where(bc => bc.DataSource == dataSource && bc.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var notesTotal = await _context
            .Notes.Where(n => n.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var notes24h = notesTotal > 0
            ? await _context
                .Notes.Where(n => n.DataSource == dataSource && n.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var deviceEventsTotal = await _context
            .DeviceEvents.Where(de => de.DataSource == dataSource)
            .LongCountAsync(cancellationToken);
        var deviceEvents24h = deviceEventsTotal > 0
            ? await _context
                .DeviceEvents.Where(de => de.DataSource == dataSource && de.Timestamp >= oneDayAgoDate)
                .CountAsync(cancellationToken)
            : 0;

        var deviceStatusTotal = await _context
            .DeviceStatuses.Where(ds => ds.Device == dataSource)
            .LongCountAsync(cancellationToken);
        var deviceStatus24h = deviceStatusTotal > 0
            ? await _context
                .DeviceStatuses.Where(ds => ds.Device == dataSource && ds.Mills >= oneDayAgo)
                .CountAsync(cancellationToken)
            : 0;

        // Build per-type breakdown dictionaries (only include non-zero types)
        var typeBreakdown = new Dictionary<string, long>();
        var typeBreakdown24h = new Dictionary<string, int>();

        // Combine legacy entries (sgv) + V4 sensor glucose into Glucose
        var glucoseTotal = (entryStats?.TotalEntries ?? 0) + sensorGlucoseTotal;
        var glucose24h = (entryStats?.EntriesLast24Hours ?? 0) + sensorGlucose24h;
        if (glucoseTotal > 0) { typeBreakdown["Glucose"] = glucoseTotal; typeBreakdown24h["Glucose"] = glucose24h; }

        if (meterGlucoseTotal > 0) { typeBreakdown["ManualBG"] = meterGlucoseTotal; typeBreakdown24h["ManualBG"] = meterGlucose24h; }
        if (bolusesTotal > 0) { typeBreakdown["Boluses"] = bolusesTotal; typeBreakdown24h["Boluses"] = boluses24h; }
        if (carbIntakesTotal > 0) { typeBreakdown["CarbIntake"] = carbIntakesTotal; typeBreakdown24h["CarbIntake"] = carbIntakes24h; }
        if (bolusCalcsTotal > 0) { typeBreakdown["BolusCalculations"] = bolusCalcsTotal; typeBreakdown24h["BolusCalculations"] = bolusCalcs24h; }
        if (notesTotal > 0) { typeBreakdown["Notes"] = notesTotal; typeBreakdown24h["Notes"] = notes24h; }
        if (deviceEventsTotal > 0) { typeBreakdown["DeviceEvents"] = deviceEventsTotal; typeBreakdown24h["DeviceEvents"] = deviceEvents24h; }

        if ((stateSpanStats?.TotalStateSpans ?? 0) > 0) { typeBreakdown["StateSpans"] = stateSpanStats!.TotalStateSpans; typeBreakdown24h["StateSpans"] = stateSpanStats.StateSpansLast24Hours; }

        if (deviceStatusTotal > 0) { typeBreakdown["DeviceStatus"] = deviceStatusTotal; typeBreakdown24h["DeviceStatus"] = deviceStatus24h; }

        // Legacy treatments that haven't been migrated to V4 tables
        if ((treatmentStats?.TotalTreatments ?? 0) > 0) { typeBreakdown["Treatments"] = treatmentStats!.TotalTreatments; typeBreakdown24h["Treatments"] = treatmentStats.TreatmentsLast24Hours; }

        // Convert timestamps
        var lastEntryTime =
            entryStats?.LastEntryMills.HasValue == true
                ? DateTimeOffset
                    .FromUnixTimeMilliseconds(entryStats.LastEntryMills.Value)
                    .UtcDateTime
                : (DateTime?)null;

        var firstEntryTime =
            entryStats?.FirstEntryMills.HasValue == true
                ? DateTimeOffset
                    .FromUnixTimeMilliseconds(entryStats.FirstEntryMills.Value)
                    .UtcDateTime
                : (DateTime?)null;

        var lastTreatmentTime =
            treatmentStats?.LastTreatmentMills.HasValue == true
                ? DateTimeOffset
                    .FromUnixTimeMilliseconds(treatmentStats.LastTreatmentMills.Value)
                    .UtcDateTime
                : (DateTime?)null;

        var firstTreatmentTime =
            treatmentStats?.FirstTreatmentMills.HasValue == true
                ? DateTimeOffset
                    .FromUnixTimeMilliseconds(treatmentStats.FirstTreatmentMills.Value)
                    .UtcDateTime
                : (DateTime?)null;

        var lastStateSpanTime = stateSpanStats?.LastStateSpanTime;
        var firstStateSpanTime = stateSpanStats?.FirstStateSpanTime;

        return new DataSourceStats(
            dataSource,
            entryStats?.TotalEntries ?? 0,
            entryStats?.EntriesLast24Hours ?? 0,
            lastEntryTime,
            firstEntryTime,
            treatmentStats?.TotalTreatments ?? 0,
            treatmentStats?.TreatmentsLast24Hours ?? 0,
            lastTreatmentTime,
            firstTreatmentTime,
            stateSpanStats?.TotalStateSpans ?? 0,
            stateSpanStats?.StateSpansLast24Hours ?? 0,
            lastStateSpanTime,
            firstStateSpanTime,
            typeBreakdown,
            typeBreakdown24h
        );
    }
}

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for Treatment operations
/// </summary>
public class TreatmentRepository : ITreatmentRepository
{
    private readonly NocturneDbContext _context;
    private readonly IQueryParser _queryParser;
    private readonly IDeduplicationService _deduplicationService;
    private readonly ILogger<TreatmentRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the TreatmentRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="queryParser">MongoDB query parser for advanced filtering</param>
    /// <param name="deduplicationService">Service for deduplicating records</param>
    /// <param name="logger">Logger instance</param>
    public TreatmentRepository(
        NocturneDbContext context,
        IQueryParser queryParser,
        IDeduplicationService deduplicationService,
        ILogger<TreatmentRepository> logger)
    {
        _context = context;
        _queryParser = queryParser;
        _deduplicationService = deduplicationService;
        _logger = logger;
    }

    /// <summary>
    /// Get treatments with optional filtering and pagination
    /// </summary>
    /// <param name="eventType">Optional event type filter.</param>
    /// <param name="count">The maximum number of treatments to return.</param>
    /// <param name="skip">The number of treatments to skip.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching treatments.</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        string? eventType = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Treatments.AsQueryable();

        // Apply event type filter if specified
        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(t => t.EventType == eventType);
        }

        // Order by Mills descending (most recent first), then apply pagination
        var entities = await query
            .OrderByDescending(t => t.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(TreatmentMapper.ToDomainModel);
    }

    /// <summary>
    /// Get a specific treatment by ID
    /// </summary>
    /// <param name="id">The unique identifier (GUID or legacy string ID).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The treatment, or null if not found.</returns>
    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Treatments.FirstOrDefaultAsync(
            t => t.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Treatments.FirstOrDefaultAsync(
                t => t.Id == guidId,
                cancellationToken
            );
        }

        return entity != null ? TreatmentMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get treatments that are meal-related within a time range
    /// </summary>
    /// <param name="from">The start timestamp.</param>
    /// <param name="to">The end timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A list of meal-related treatments.</returns>
    public async Task<IReadOnlyList<Treatment>> GetMealTreatmentsInTimeRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var fromMills = from.ToUnixTimeMilliseconds();
        var toMills = to.ToUnixTimeMilliseconds();

        var entities = await _context.Treatments
            .Where(t => t.Mills >= fromMills && t.Mills <= toMills)
            .Where(t => t.Carbs > 0 || (t.EventType != null && t.EventType.Contains("Meal")))
            .OrderBy(t => t.Mills)
            .ToListAsync(cancellationToken);

        return entities.Select(TreatmentMapper.ToDomainModel).ToList();
    }

    /// <summary>
    /// Get all treatments within a time range
    /// </summary>
    /// <param name="startMills">The start timestamp in unix milliseconds.</param>
    /// <param name="endMills">The end timestamp in unix milliseconds.</param>
    /// <param name="count">The maximum number of treatments to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of treatments within the range.</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsByTimeRangeAsync(
        long startMills,
        long endMills,
        int count = 10000,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.Treatments
            .Where(t => t.Mills >= startMills && t.Mills <= endMills)
            .OrderByDescending(t => t.Mills)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(TreatmentMapper.ToDomainModel);
    }

    /// <summary>
    /// Create a single treatment and link to canonical groups for deduplication
    /// </summary>
    /// <param name="treatment">The treatment data to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created treatment.</returns>
    public async Task<Treatment?> CreateTreatmentAsync(
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        var results = await CreateTreatmentsAsync([treatment], cancellationToken);
        return results.FirstOrDefault();
    }

    /// <summary>
    /// Create new treatments and link to canonical groups for deduplication
    /// </summary>
    /// <param name="treatments">The collection of treatments to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of created or updated treatments.</returns>
    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var entities = treatments.Select(TreatmentMapper.ToEntity).ToList();
        var resultEntities = new List<TreatmentEntity>();
        var newEntities = new List<TreatmentEntity>();

        const int batchSize = 500;
        foreach (var batchEntities in entities.Chunk(batchSize))
        {
            foreach (var entity in batchEntities)
            {
                // Check if a treatment with this ID already exists
                var existingEntity = await _context.Treatments.FirstOrDefaultAsync(
                    t => t.Id == entity.Id,
                    cancellationToken
                );

                if (existingEntity != null)
                {
                    // Update existing entity instead of inserting a duplicate
                    _context.Entry(existingEntity).CurrentValues.SetValues(entity);
                    resultEntities.Add(existingEntity);
                }
                else
                {
                    // Add new entity
                    _context.Treatments.Add(entity);
                    resultEntities.Add(entity);
                    newEntities.Add(entity);
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            _context.ChangeTracker.Clear();
        }

        // Link new treatments to canonical groups for deduplication
        foreach (var entity in newEntities)
        {
            try
            {
                var criteria = new MatchCriteria
                {
                    EventType = entity.EventType,
                    Insulin = entity.Insulin,
                    InsulinTolerance = 0.1, // 0.1 unit tolerance
                    Carbs = entity.Carbs,
                    CarbsTolerance = 1.0 // 1g tolerance
                };

                var canonicalId = await _deduplicationService.GetOrCreateCanonicalIdAsync(
                    RecordType.Treatment,
                    entity.Mills,
                    criteria,
                    cancellationToken);

                await _deduplicationService.LinkRecordAsync(
                    canonicalId,
                    RecordType.Treatment,
                    entity.Id,
                    entity.Mills,
                    entity.DataSource ?? "unknown",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Don't fail the insert if deduplication fails
                _logger.LogWarning(ex, "Failed to deduplicate treatment {TreatmentId}", entity.Id);
            }
        }

        return resultEntities.Select(TreatmentMapper.ToDomainModel);
    }

    /// <summary>
    /// Update an existing treatment
    /// </summary>
    /// <param name="id">The unique identifier of the treatment to update.</param>
    /// <param name="treatment">The updated treatment data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated treatment, or null if not found.</returns>
    public async Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Treatments.FirstOrDefaultAsync(
            t => t.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Treatments.FirstOrDefaultAsync(
                t => t.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
            return null;

        TreatmentMapper.UpdateEntity(entity, treatment);
        await _context.SaveChangesAsync(cancellationToken);

        return TreatmentMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete a treatment
    /// </summary>
    /// <param name="id">The unique identifier of the treatment to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the treatment was deleted, otherwise false.</returns>
    public async Task<bool> DeleteTreatmentAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        var entity = await _context.Treatments.FirstOrDefaultAsync(
            t => t.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Treatments.FirstOrDefaultAsync(
                t => t.Id == guidId,
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
    /// Delete multiple treatments with optional filtering
    /// </summary>
    /// <param name="eventType">Optional event type filter for deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of treatments deleted.</returns>
    public async Task<long> DeleteTreatmentsAsync(
        string? eventType = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Treatments.AsQueryable();

        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(t => t.EventType == eventType);
        }

        var now = DateTime.UtcNow;
        var deletedCount = await query.ExecuteUpdateAsync(
            s => s
                .SetProperty(t => t.DeletedAt, now)
                .SetProperty(t => t.SysUpdatedAt, now),
            cancellationToken
        );
        return deletedCount;
    }

    /// <summary>
    /// Delete all treatments with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source to filter by (e.g., "demo-service")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The number of treatments deleted</returns>
    public async Task<long> DeleteByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var now = DateTime.UtcNow;
        var deletedCount = await _context
            .Treatments.Where(t => t.DataSource == dataSource)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(t => t.DeletedAt, now)
                    .SetProperty(t => t.SysUpdatedAt, now),
                cancellationToken
            );
        return deletedCount;
    }

    /// <summary>
    /// Get treatments with advanced filtering
    /// </summary>
    /// <param name="eventType">Optional event type filter.</param>
    /// <param name="count">The maximum number of treatments to return.</param>
    /// <param name="skip">The number of treatments to skip.</param>
    /// <param name="findQuery">Optional MongoDB-style search query string.</param>
    /// <param name="dateString">Optional date string filter.</param>
    /// <param name="reverseResults">Whether to reverse the order of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching treatments.</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        string? eventType = null,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? dateString = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Treatments.AsQueryable();

        // Apply event type filter if specified
        if (!string.IsNullOrEmpty(eventType))
        {
            query = query.Where(t => t.EventType == eventType);
        }

        // Apply date filter if specified
        if (!string.IsNullOrEmpty(dateString) && DateTime.TryParse(dateString, out var filterDate))
        {
            var filterMills = ((DateTimeOffset)filterDate).ToUnixTimeMilliseconds();
            query = query.Where(t => t.Mills >= filterMills);
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
            query = query.OrderBy(t => t.Mills);
        }
        else
        {
            query = query.OrderByDescending(t => t.Mills);
        }

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(TreatmentMapper.ToDomainModel);
    }

    /// <summary>
    /// Count treatments with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional search query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of matching treatments.</returns>
    public async Task<long> CountTreatmentsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Treatments.AsQueryable();

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
    /// Get treatments with pagination (interface-compatible overload without eventType)
    /// </summary>
    async Task<IEnumerable<Treatment>> ITreatmentRepository.GetTreatmentsAsync(
        int count,
        int skip,
        CancellationToken cancellationToken
    )
    {
        return await GetTreatmentsAsync(null, count, skip, cancellationToken);
    }

    /// <summary>
    /// Get treatments with advanced filtering (interface-compatible overload without eventType/dateString)
    /// </summary>
    async Task<IEnumerable<Treatment>> ITreatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
        int count,
        int skip,
        string? findQuery,
        bool reverseResults,
        CancellationToken cancellationToken
    )
    {
        return await GetTreatmentsWithAdvancedFilterAsync(
            null, count, skip, findQuery, null, reverseResults, cancellationToken);
    }

    /// <summary>
    /// Get treatments with advanced filtering including event type (interface-compatible overload without dateString)
    /// </summary>
    async Task<IEnumerable<Treatment>> ITreatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
        string? eventType,
        int count,
        int skip,
        string? findQuery,
        bool reverseResults,
        CancellationToken cancellationToken
    )
    {
        return await GetTreatmentsWithAdvancedFilterAsync(
            eventType, count, skip, findQuery, null, reverseResults, cancellationToken);
    }

    /// <summary>
    /// Get the latest treatment timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The source identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The timestamp of the latest treatment, or null if none exist.</returns>
    public async Task<DateTime?> GetLatestTreatmentTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var latestTreatment = await _context
            .Treatments.Where(t => t.DataSource == dataSource)
            .OrderByDescending(t => t.Mills)
            .Select(t => new { t.Mills })
            .FirstOrDefaultAsync(cancellationToken);

        if (latestTreatment == null)
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds(latestTreatment.Mills).UtcDateTime;
    }

    /// <summary>
    /// Get the oldest treatment timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The source identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The timestamp of the oldest treatment, or null if none exist.</returns>
    public async Task<DateTime?> GetOldestTreatmentTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        var oldestTreatment = await _context
            .Treatments.Where(t => t.DataSource == dataSource)
            .OrderBy(t => t.Mills)
            .Select(t => new { t.Mills })
            .FirstOrDefaultAsync(cancellationToken);

        if (oldestTreatment == null)
            return null;

        return DateTimeOffset.FromUnixTimeMilliseconds(oldestTreatment.Mills).UtcDateTime;
    }

    /// <summary>
    /// Check for duplicate treatment in the database by ID or OriginalId
    /// </summary>
    /// <param name="id">The primary identifier of the treatment.</param>
    /// <param name="originalId">The legacy MongoDB identifier of the treatment.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The duplicate treatment if found, otherwise null.</returns>
    public async Task<Treatment?> CheckForDuplicateTreatmentAsync(
        string? id,
        string? originalId,
        CancellationToken cancellationToken = default
    )
    {
        // If both are null/empty, no duplicate check possible
        if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(originalId))
            return null;

        // Try to find by OriginalId first (MongoDB ObjectId), then by GUID
        Treatment? duplicate = null;

        if (!string.IsNullOrEmpty(originalId))
        {
            duplicate = await GetTreatmentByIdAsync(originalId, cancellationToken);
            if (duplicate != null)
                return duplicate;
        }

        if (!string.IsNullOrEmpty(id))
        {
            duplicate = await GetTreatmentByIdAsync(id, cancellationToken);
            if (duplicate != null)
                return duplicate;
        }

        return null;
    }

    /// <summary>
    /// Delete all treatments with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source to filter by.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of records deleted.</returns>
    public async Task<long> DeleteTreatmentsByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    )
    {
        return await DeleteByDataSourceAsync(dataSource, cancellationToken);
    }

    /// <summary>
    /// Bulk delete treatments using query filters
    /// </summary>
    /// <param name="findQuery">The search query for deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of records deleted.</returns>
    public async Task<long> BulkDeleteTreatmentsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        // Parse eventType from the find query (supports find[eventType]=Value format)
        string? eventType = null;
        if (!string.IsNullOrEmpty(findQuery))
        {
            var decodedQuery = System.Web.HttpUtility.UrlDecode(findQuery);
            var match = System.Text.RegularExpressions.Regex.Match(
                decodedQuery,
                @"find\[eventType\]=([^&]+)",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                eventType = match.Groups[1].Value;
            }
        }

        return await DeleteTreatmentsAsync(eventType, cancellationToken);
    }

    /// <summary>
    /// Get treatments modified since a given timestamp (for incremental sync)
    /// </summary>
    /// <param name="lastModifiedMills">The last modified timestamp in unix milliseconds.</param>
    /// <param name="limit">The maximum number of treatments to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of modified treatments.</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    )
    {
        var threshold = DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMills).UtcDateTime;
        var entities = await _context
            .Treatments.IgnoreQueryFilters()
            .Where(t => t.TenantId == _context.TenantId)
            .Where(t => t.SysUpdatedAt >= threshold)
            .OrderBy(t => t.SysUpdatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return entities.Select(TreatmentMapper.ToDomainModel);
    }

    /// <summary>
    /// Patch a treatment by ID using JSON merge-patch semantics
    /// </summary>
    /// <param name="id">The unique identifier of the treatment to patch.</param>
    /// <param name="patchData">The JSON patch data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The patched treatment, or null if not found.</returns>
    public async Task<Treatment?> PatchTreatmentAsync(
        string id,
        JsonElement patchData,
        CancellationToken cancellationToken = default
    )
    {
        // Look up entity using same pattern: OriginalId first, then GUID
        var entity = await _context.Treatments.FirstOrDefaultAsync(
            t => t.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Treatments.FirstOrDefaultAsync(
                t => t.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
            return null;

        // Convert entity to domain model, serialize to JSON, merge patch on top
        var existing = TreatmentMapper.ToDomainModel(entity);
        var existingJson = JsonSerializer.SerializeToNode(existing);

        if (existingJson is JsonObject existingObj)
        {
            foreach (var property in patchData.EnumerateObject())
            {
                existingObj[property.Name] = JsonNode.Parse(property.Value.GetRawText());
            }
        }

        var patched = existingJson!.Deserialize<Treatment>();
        if (patched == null)
            return null;

        TreatmentMapper.UpdateEntity(entity, patched);
        await _context.SaveChangesAsync(cancellationToken);

        return TreatmentMapper.ToDomainModel(entity);
    }
}

using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for DeviceStatus operations
/// </summary>
public class DeviceStatusRepository : IDeviceStatusRepository
{
    private readonly NocturneDbContext _context;
    private readonly IQueryParser _queryParser;

    private static readonly Dictionary<string, Func<string, object>> DeviceStatusConverters = new()
    {
        ["created_at"] = ParseIsoDateToMills,
        ["date"] = ParseIsoDateToMills,
        ["mills"] = ParseIsoDateToMills,
        ["device"] = s => s.Trim('\'', '"'),
    };

    private static object ParseIsoDateToMills(string value)
    {
        // Handle ISO 8601 date strings and convert to epoch milliseconds
        if (DateTimeOffset.TryParse(value, out var dateTime))
        {
            return dateTime.ToUnixTimeMilliseconds();
        }

        // If it's already a number (mills), parse it directly
        if (long.TryParse(value, out var mills))
        {
            return mills;
        }

        // Fallback: try trimming quotes
        var trimmed = value.Trim('\'', '"');
        if (DateTimeOffset.TryParse(trimmed, out dateTime))
        {
            return dateTime.ToUnixTimeMilliseconds();
        }

        return value;
    }

    /// <summary>
    /// Initializes a new instance of the DeviceStatusRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    /// <param name="queryParser">MongoDB query parser for advanced filtering</param>
    public DeviceStatusRepository(NocturneDbContext context, IQueryParser queryParser)
    {
        _context = context;
        _queryParser = queryParser;
    }

    /// <summary>
    /// Get device status entries with optional filtering and pagination
    /// </summary>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <param name="skip">The number of entries to skip.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of device status records.</returns>
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Order by Mills descending (most recent first), then apply pagination
        var entities = await _context
            .DeviceStatuses.OrderByDescending(ds => ds.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Get a specific device status by ID
    /// </summary>
    /// <param name="id">The unique identifier (GUID or legacy string ID).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The device status, or null if not found.</returns>
    public async Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            // Try to find by GUID ID first
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            // Try to find by original MongoDB ID
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        return entity != null ? DeviceStatusMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get device status entries with advanced filtering support including find queries and reverse ordering
    /// </summary>
    /// <param name="count">The maximum number of entries to return.</param>
    /// <param name="skip">The number of entries to skip.</param>
    /// <param name="findQuery">Optional MongoDB-style find query string.</param>
    /// <param name="reverseResults">Whether to reverse the order of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching device status records.</returns>
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply advanced MongoDB-style query filtering using QueryParser
        if (!string.IsNullOrEmpty(findQuery))
        {
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
                TypeConverters = DeviceStatusConverters,
                DisableDefaultDateFilter = true, // Device status queries don't auto-filter by date
            };

            query = await _queryParser.ApplyQueryAsync(
                query,
                findQuery,
                options,
                cancellationToken
            );
        }

        // Apply ordering
        if (reverseResults)
        {
            query = query.OrderBy(ds => ds.Mills);
        }
        else
        {
            query = query.OrderByDescending(ds => ds.Mills);
        }

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Create multiple device status entries, skipping duplicates
    /// </summary>
    /// <param name="deviceStatuses">The collection of device status records to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of created device status records.</returns>
    public async Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        CancellationToken cancellationToken = default
    )
    {
        var entities = deviceStatuses.Select(DeviceStatusMapper.ToEntity).ToList();

        if (entities.Count == 0)
        {
            return Enumerable.Empty<DeviceStatus>();
        }

        // Get the IDs of the entities we're trying to insert
        var entityIds = entities.Select(e => e.Id).ToHashSet();

        // Check which IDs already exist in the database
        var existingIds = await _context
            .DeviceStatuses.Where(ds => entityIds.Contains(ds.Id))
            .Select(ds => ds.Id)
            .ToHashSetAsync(cancellationToken);

        // Filter out entities that already exist
        var newEntities = entities.Where(e => !existingIds.Contains(e.Id)).ToList();

        if (newEntities.Count == 0)
        {
            // All entries already exist, return empty
            return Enumerable.Empty<DeviceStatus>();
        }

        await _context.DeviceStatuses.AddRangeAsync(newEntities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return newEntities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Update an existing device status by ID
    /// </summary>
    /// <param name="id">The unique identifier of the device status to update.</param>
    /// <param name="deviceStatus">The updated device status data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated device status, or null if not found.</returns>
    public async Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        if (entity == null)
            return null;

        DeviceStatusMapper.UpdateEntity(entity, deviceStatus);
        await _context.SaveChangesAsync(cancellationToken);

        return DeviceStatusMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete a device status by ID
    /// </summary>
    /// <param name="id">The unique identifier of the device status to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the device status was deleted, otherwise false.</returns>
    public async Task<bool> DeleteDeviceStatusAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        if (entity == null)
            return false;

        _context.DeviceStatuses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Bulk delete device status entries using query filters
    /// </summary>
    /// <param name="findQuery">The filter criteria for deletion.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of deleted records.</returns>
    public async Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply advanced MongoDB-style query filtering using QueryParser
        if (!string.IsNullOrEmpty(findQuery))
        {
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
                TypeConverters = DeviceStatusConverters,
                DisableDefaultDateFilter = true,
            };

            query = await _queryParser.ApplyQueryAsync(
                query,
                findQuery,
                options,
                cancellationToken
            );
        }

        var entitiesToDelete = await query.ToListAsync(cancellationToken);
        var deletedCount = entitiesToDelete.Count;

        if (deletedCount > 0)
        {
            _context.DeviceStatuses.RemoveRange(entitiesToDelete);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return deletedCount;
    }

    /// <summary>
    /// Count device status entries matching specific criteria
    /// </summary>
    /// <param name="findQuery">Optional MongoDB-style find query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of matching device status records.</returns>
    public async Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply advanced MongoDB-style query filtering using QueryParser
        if (!string.IsNullOrEmpty(findQuery))
        {
            var options = new QueryOptions
            {
                DateField = "Mills",
                UseEpochDates = true,
                DefaultDateRange = TimeSpan.FromDays(4),
                TypeConverters = DeviceStatusConverters,
                DisableDefaultDateFilter = true,
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
    /// Get device status records modified since a given timestamp (for incremental sync)
    /// </summary>
    /// <param name="lastModifiedMills">The timestamp in unix milliseconds.</param>
    /// <param name="limit">The maximum number of entries to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of device status records modified since the timestamp.</returns>
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    )
    {
        var threshold = DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMills).UtcDateTime;
        var entities = await _context
            .DeviceStatuses.Where(d => d.SysUpdatedAt >= threshold)
            .OrderBy(d => d.SysUpdatedAt)
            .Take(limit)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }
}

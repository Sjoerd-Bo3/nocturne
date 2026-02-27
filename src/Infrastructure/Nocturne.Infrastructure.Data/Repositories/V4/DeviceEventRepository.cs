using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class DeviceEventRepository : IDeviceEventRepository
{
    private readonly NocturneDbContext _context;
    private readonly IDeduplicationService _deduplicationService;
    private readonly ILogger<DeviceEventRepository> _logger;

    public DeviceEventRepository(
        NocturneDbContext context,
        IDeduplicationService deduplicationService,
        ILogger<DeviceEventRepository> logger)
    {
        _context = context;
        _deduplicationService = deduplicationService;
        _logger = logger;
    }

    public async Task<IEnumerable<DeviceEvent>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        bool nativeOnly = false,
        CancellationToken ct = default
    )
    {
        var query = _context.DeviceEvents.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        if (device != null)
            query = query.Where(e => e.Device == device);
        if (source != null)
            query = query.Where(e => e.DataSource == source);
        if (nativeOnly)
            query = query.Where(e => e.LegacyId == null);
        query = descending ? query.OrderByDescending(e => e.Timestamp) : query.OrderBy(e => e.Timestamp);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(ct);
        return entities.Select(DeviceEventMapper.ToDomainModel);
    }

    public async Task<DeviceEvent?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.DeviceEvents.FindAsync([id], ct);
        return entity is null ? null : DeviceEventMapper.ToDomainModel(entity);
    }

    public async Task<DeviceEvent?> GetByLegacyIdAsync(
        string legacyId,
        CancellationToken ct = default
    )
    {
        var entity = await _context.DeviceEvents.FirstOrDefaultAsync(
            e => e.LegacyId == legacyId,
            ct
        );
        return entity is null ? null : DeviceEventMapper.ToDomainModel(entity);
    }

    public async Task<DeviceEvent> CreateAsync(DeviceEvent model, CancellationToken ct = default)
    {
        var entity = DeviceEventMapper.ToEntity(model);
        _context.DeviceEvents.Add(entity);
        await _context.SaveChangesAsync(ct);
        return DeviceEventMapper.ToDomainModel(entity);
    }

    public async Task<DeviceEvent> UpdateAsync(
        Guid id,
        DeviceEvent model,
        CancellationToken ct = default
    )
    {
        var entity =
            await _context.DeviceEvents.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"DeviceEvent {id} not found");
        DeviceEventMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return DeviceEventMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.DeviceEvents.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"DeviceEvent {id} not found");
        _context.DeviceEvents.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.DeviceEvents.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<DeviceEvent>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .DeviceEvents.AsNoTracking()
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync(ct);
        return entities.Select(DeviceEventMapper.ToDomainModel);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context
            .DeviceEvents.Where(e => e.LegacyId == legacyId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<DeviceEvent>> BulkCreateAsync(
        IEnumerable<DeviceEvent> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(DeviceEventMapper.ToEntity).ToList();
        if (entities.Count == 0)
            return [];

        // Batch-level dedup: keep first occurrence per LegacyId
        entities = entities
            .GroupBy(e => e.LegacyId ?? e.Id.ToString())
            .Select(g => g.First())
            .ToList();

        // DB-level dedup: filter out records whose LegacyId already exists
        var legacyIds = entities
            .Where(e => !string.IsNullOrEmpty(e.LegacyId))
            .Select(e => e.LegacyId!)
            .ToHashSet();

        if (legacyIds.Count > 0)
        {
            var existingIds = await _context
                .DeviceEvents.AsNoTracking()
                .Where(e => legacyIds.Contains(e.LegacyId!))
                .Select(e => e.LegacyId)
                .ToListAsync(ct);

            var existingSet = existingIds.ToHashSet();
            entities = entities
                .Where(e => string.IsNullOrEmpty(e.LegacyId) || !existingSet.Contains(e.LegacyId))
                .ToList();
        }

        if (entities.Count == 0)
            return [];

        const int batchSize = 500;
        foreach (var batch in entities.Chunk(batchSize))
        {
            _context.DeviceEvents.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        // Insert-time deduplication: link saved records to canonical groups
        foreach (var entity in entities)
        {
            try
            {
                var criteria = new MatchCriteria
                {
                    EventType = entity.EventType
                };

                var canonicalId = await _deduplicationService.GetOrCreateCanonicalIdAsync(
                    RecordType.DeviceEvent,
                    new DateTimeOffset(entity.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                    criteria,
                    ct);

                await _deduplicationService.LinkRecordAsync(
                    canonicalId,
                    RecordType.DeviceEvent,
                    entity.Id,
                    new DateTimeOffset(entity.Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                    entity.DataSource ?? "unknown",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deduplicate DeviceEvent {Id}", entity.Id);
            }
        }

        return entities.Select(DeviceEventMapper.ToDomainModel);
    }
}

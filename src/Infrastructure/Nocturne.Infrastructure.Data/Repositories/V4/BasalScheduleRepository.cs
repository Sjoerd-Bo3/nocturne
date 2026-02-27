using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class BasalScheduleRepository : IBasalScheduleRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<BasalScheduleRepository> _logger;

    public BasalScheduleRepository(NocturneDbContext context, ILogger<BasalScheduleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<BasalSchedule>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    )
    {
        var query = _context.BasalSchedules.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        if (device != null)
            query = query.Where(e => e.Device == device);
        if (source != null)
            query = query.Where(e => e.DataSource == source);
        query = descending ? query.OrderByDescending(e => e.Timestamp) : query.OrderBy(e => e.Timestamp);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(ct);
        return entities.Select(BasalScheduleMapper.ToDomainModel);
    }

    public async Task<BasalSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.BasalSchedules.FindAsync([id], ct);
        return entity is null ? null : BasalScheduleMapper.ToDomainModel(entity);
    }

    public async Task<BasalSchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.BasalSchedules.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : BasalScheduleMapper.ToDomainModel(entity);
    }

    public async Task<IEnumerable<BasalSchedule>> GetByProfileNameAsync(
        string profileName,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .BasalSchedules.AsNoTracking()
            .Where(e => e.ProfileName == profileName)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(ct);
        return entities.Select(BasalScheduleMapper.ToDomainModel);
    }

    public async Task<BasalSchedule> CreateAsync(BasalSchedule model, CancellationToken ct = default)
    {
        var entity = BasalScheduleMapper.ToEntity(model);
        _context.BasalSchedules.Add(entity);
        await _context.SaveChangesAsync(ct);
        return BasalScheduleMapper.ToDomainModel(entity);
    }

    public async Task<BasalSchedule> UpdateAsync(Guid id, BasalSchedule model, CancellationToken ct = default)
    {
        var entity =
            await _context.BasalSchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"BasalSchedule {id} not found");
        BasalScheduleMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return BasalScheduleMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.BasalSchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"BasalSchedule {id} not found");
        _context.BasalSchedules.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.BasalSchedules.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return await _context
            .BasalSchedules.Where(e => e.LegacyId != null && e.LegacyId.StartsWith(prefix))
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.BasalSchedules.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<BasalSchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .BasalSchedules.AsNoTracking()
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync(ct);
        return entities.Select(BasalScheduleMapper.ToDomainModel);
    }

    public async Task<IEnumerable<BasalSchedule>> BulkCreateAsync(
        IEnumerable<BasalSchedule> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(BasalScheduleMapper.ToEntity).ToList();
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
                .BasalSchedules.AsNoTracking()
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
            _context.BasalSchedules.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(BasalScheduleMapper.ToDomainModel);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class TargetRangeScheduleRepository : ITargetRangeScheduleRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<TargetRangeScheduleRepository> _logger;

    public TargetRangeScheduleRepository(NocturneDbContext context, ILogger<TargetRangeScheduleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TargetRangeSchedule>> GetAsync(
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
        var query = _context.TargetRangeSchedules.AsNoTracking().AsQueryable();
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
        return entities.Select(TargetRangeScheduleMapper.ToDomainModel);
    }

    public async Task<TargetRangeSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TargetRangeSchedules.FindAsync([id], ct);
        return entity is null ? null : TargetRangeScheduleMapper.ToDomainModel(entity);
    }

    public async Task<TargetRangeSchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.TargetRangeSchedules.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : TargetRangeScheduleMapper.ToDomainModel(entity);
    }

    public async Task<IEnumerable<TargetRangeSchedule>> GetByProfileNameAsync(
        string profileName,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .TargetRangeSchedules.AsNoTracking()
            .Where(e => e.ProfileName == profileName)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(ct);
        return entities.Select(TargetRangeScheduleMapper.ToDomainModel);
    }

    public async Task<TargetRangeSchedule> CreateAsync(TargetRangeSchedule model, CancellationToken ct = default)
    {
        var entity = TargetRangeScheduleMapper.ToEntity(model);
        _context.TargetRangeSchedules.Add(entity);
        await _context.SaveChangesAsync(ct);
        return TargetRangeScheduleMapper.ToDomainModel(entity);
    }

    public async Task<TargetRangeSchedule> UpdateAsync(
        Guid id,
        TargetRangeSchedule model,
        CancellationToken ct = default
    )
    {
        var entity =
            await _context.TargetRangeSchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TargetRangeSchedule {id} not found");
        TargetRangeScheduleMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return TargetRangeScheduleMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.TargetRangeSchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TargetRangeSchedule {id} not found");
        _context.TargetRangeSchedules.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.TargetRangeSchedules.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return await _context
            .TargetRangeSchedules.Where(e => e.LegacyId != null && e.LegacyId.StartsWith(prefix))
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var query = _context.TargetRangeSchedules.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<TargetRangeSchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .TargetRangeSchedules.AsNoTracking()
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync(ct);
        return entities.Select(TargetRangeScheduleMapper.ToDomainModel);
    }

    public async Task<IEnumerable<TargetRangeSchedule>> BulkCreateAsync(
        IEnumerable<TargetRangeSchedule> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(TargetRangeScheduleMapper.ToEntity).ToList();
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
                .TargetRangeSchedules.AsNoTracking()
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
            _context.TargetRangeSchedules.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(TargetRangeScheduleMapper.ToDomainModel);
    }
}

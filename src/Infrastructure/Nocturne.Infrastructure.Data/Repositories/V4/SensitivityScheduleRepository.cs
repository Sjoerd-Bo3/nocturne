using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class SensitivityScheduleRepository : ISensitivityScheduleRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<SensitivityScheduleRepository> _logger;

    public SensitivityScheduleRepository(NocturneDbContext context, ILogger<SensitivityScheduleRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<SensitivitySchedule>> GetAsync(
        long? from,
        long? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    )
    {
        var query = _context.SensitivitySchedules.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Mills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Mills <= to.Value);
        if (device != null)
            query = query.Where(e => e.Device == device);
        if (source != null)
            query = query.Where(e => e.DataSource == source);
        query = descending ? query.OrderByDescending(e => e.Mills) : query.OrderBy(e => e.Mills);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(ct);
        return entities.Select(SensitivityScheduleMapper.ToDomainModel);
    }

    public async Task<SensitivitySchedule?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.SensitivitySchedules.FindAsync([id], ct);
        return entity is null ? null : SensitivityScheduleMapper.ToDomainModel(entity);
    }

    public async Task<SensitivitySchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.SensitivitySchedules.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : SensitivityScheduleMapper.ToDomainModel(entity);
    }

    public async Task<IEnumerable<SensitivitySchedule>> GetByProfileNameAsync(
        string profileName,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .SensitivitySchedules.AsNoTracking()
            .Where(e => e.ProfileName == profileName)
            .OrderByDescending(e => e.Mills)
            .ToListAsync(ct);
        return entities.Select(SensitivityScheduleMapper.ToDomainModel);
    }

    public async Task<SensitivitySchedule> CreateAsync(SensitivitySchedule model, CancellationToken ct = default)
    {
        var entity = SensitivityScheduleMapper.ToEntity(model);
        _context.SensitivitySchedules.Add(entity);
        await _context.SaveChangesAsync(ct);
        return SensitivityScheduleMapper.ToDomainModel(entity);
    }

    public async Task<SensitivitySchedule> UpdateAsync(
        Guid id,
        SensitivitySchedule model,
        CancellationToken ct = default
    )
    {
        var entity =
            await _context.SensitivitySchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"SensitivitySchedule {id} not found");
        SensitivityScheduleMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return SensitivityScheduleMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.SensitivitySchedules.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"SensitivitySchedule {id} not found");
        _context.SensitivitySchedules.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.SensitivitySchedules.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return await _context
            .SensitivitySchedules.Where(e => e.LegacyId != null && e.LegacyId.StartsWith(prefix))
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> CountAsync(long? from, long? to, CancellationToken ct = default)
    {
        var query = _context.SensitivitySchedules.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Mills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Mills <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<SensitivitySchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .SensitivitySchedules.AsNoTracking()
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync(ct);
        return entities.Select(SensitivityScheduleMapper.ToDomainModel);
    }

    public async Task<IEnumerable<SensitivitySchedule>> BulkCreateAsync(
        IEnumerable<SensitivitySchedule> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(SensitivityScheduleMapper.ToEntity).ToList();
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
                .SensitivitySchedules.AsNoTracking()
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
            _context.SensitivitySchedules.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(SensitivityScheduleMapper.ToDomainModel);
    }
}

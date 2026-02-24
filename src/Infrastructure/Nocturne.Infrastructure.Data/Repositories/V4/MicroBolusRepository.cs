using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class MicroBolusRepository : IMicroBolusRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<MicroBolusRepository> _logger;

    public MicroBolusRepository(
        NocturneDbContext context,
        ILogger<MicroBolusRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<MicroBolus>> GetAsync(
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
        var query = _context.MicroBoluses.AsNoTracking().AsQueryable();
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
        return entities.Select(MicroBolusMapper.ToDomainModel);
    }

    public async Task<MicroBolus?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.MicroBoluses.FindAsync([id], ct);
        return entity is null ? null : MicroBolusMapper.ToDomainModel(entity);
    }

    public async Task<MicroBolus?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.MicroBoluses.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : MicroBolusMapper.ToDomainModel(entity);
    }

    public async Task<MicroBolus> CreateAsync(MicroBolus model, CancellationToken ct = default)
    {
        var entity = MicroBolusMapper.ToEntity(model);
        _context.MicroBoluses.Add(entity);
        await _context.SaveChangesAsync(ct);
        return MicroBolusMapper.ToDomainModel(entity);
    }

    public async Task<MicroBolus> UpdateAsync(Guid id, MicroBolus model, CancellationToken ct = default)
    {
        var entity =
            await _context.MicroBoluses.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"MicroBolus {id} not found");
        MicroBolusMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return MicroBolusMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.MicroBoluses.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"MicroBolus {id} not found");
        _context.MicroBoluses.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> CountAsync(long? from, long? to, CancellationToken ct = default)
    {
        var query = _context.MicroBoluses.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Mills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Mills <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.MicroBoluses.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<IEnumerable<MicroBolus>> BulkCreateAsync(
        IEnumerable<MicroBolus> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(MicroBolusMapper.ToEntity).ToList();
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
                .MicroBoluses.AsNoTracking()
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
            _context.MicroBoluses.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(MicroBolusMapper.ToDomainModel);
    }
}

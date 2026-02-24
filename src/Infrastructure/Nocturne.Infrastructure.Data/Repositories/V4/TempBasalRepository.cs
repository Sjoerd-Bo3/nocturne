using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class TempBasalRepository : ITempBasalRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<TempBasalRepository> _logger;

    public TempBasalRepository(
        NocturneDbContext context,
        ILogger<TempBasalRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TempBasal>> GetAsync(
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
        var query = _context.TempBasals.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.StartMills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.StartMills <= to.Value);
        if (device != null)
            query = query.Where(e => e.Device == device);
        if (source != null)
            query = query.Where(e => e.DataSource == source);
        query = descending
            ? query.OrderByDescending(e => e.StartMills)
            : query.OrderBy(e => e.StartMills);
        var entities = await query.Skip(offset).Take(limit).ToListAsync(ct);
        return entities.Select(TempBasalMapper.ToDomainModel);
    }

    public async Task<TempBasal?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TempBasals.FindAsync([id], ct);
        return entity is null ? null : TempBasalMapper.ToDomainModel(entity);
    }

    public async Task<TempBasal?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.TempBasals.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : TempBasalMapper.ToDomainModel(entity);
    }

    public async Task<TempBasal> CreateAsync(TempBasal model, CancellationToken ct = default)
    {
        var entity = TempBasalMapper.ToEntity(model);
        _context.TempBasals.Add(entity);
        await _context.SaveChangesAsync(ct);
        return TempBasalMapper.ToDomainModel(entity);
    }

    public async Task<TempBasal> UpdateAsync(Guid id, TempBasal model, CancellationToken ct = default)
    {
        var entity =
            await _context.TempBasals.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TempBasal {id} not found");
        TempBasalMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return TempBasalMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.TempBasals.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TempBasal {id} not found");
        _context.TempBasals.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.TempBasals.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<int> CountAsync(long? from, long? to, CancellationToken ct = default)
    {
        var query = _context.TempBasals.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.StartMills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.StartMills <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<TempBasal>> BulkCreateAsync(
        IEnumerable<TempBasal> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(TempBasalMapper.ToEntity).ToList();
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
                .TempBasals.AsNoTracking()
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
            _context.TempBasals.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(TempBasalMapper.ToDomainModel);
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class TherapySettingsRepository : ITherapySettingsRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<TherapySettingsRepository> _logger;

    public TherapySettingsRepository(NocturneDbContext context, ILogger<TherapySettingsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<TherapySettings>> GetAsync(
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
        var query = _context.TherapySettings.AsNoTracking().AsQueryable();
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
        return entities.Select(TherapySettingsMapper.ToDomainModel);
    }

    public async Task<TherapySettings?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.TherapySettings.FindAsync([id], ct);
        return entity is null ? null : TherapySettingsMapper.ToDomainModel(entity);
    }

    public async Task<TherapySettings?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var entity = await _context.TherapySettings.FirstOrDefaultAsync(e => e.LegacyId == legacyId, ct);
        return entity is null ? null : TherapySettingsMapper.ToDomainModel(entity);
    }

    public async Task<IEnumerable<TherapySettings>> GetByProfileNameAsync(
        string profileName,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .TherapySettings.AsNoTracking()
            .Where(e => e.ProfileName == profileName)
            .OrderByDescending(e => e.Mills)
            .ToListAsync(ct);
        return entities.Select(TherapySettingsMapper.ToDomainModel);
    }

    public async Task<TherapySettings> CreateAsync(TherapySettings model, CancellationToken ct = default)
    {
        var entity = TherapySettingsMapper.ToEntity(model);
        _context.TherapySettings.Add(entity);
        await _context.SaveChangesAsync(ct);
        return TherapySettingsMapper.ToDomainModel(entity);
    }

    public async Task<TherapySettings> UpdateAsync(Guid id, TherapySettings model, CancellationToken ct = default)
    {
        var entity =
            await _context.TherapySettings.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TherapySettings {id} not found");
        TherapySettingsMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return TherapySettingsMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity =
            await _context.TherapySettings.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"TherapySettings {id} not found");
        _context.TherapySettings.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        return await _context.TherapySettings.Where(e => e.LegacyId == legacyId).ExecuteDeleteAsync(ct);
    }

    public async Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default)
    {
        return await _context
            .TherapySettings.Where(e => e.LegacyId != null && e.LegacyId.StartsWith(prefix))
            .ExecuteDeleteAsync(ct);
    }

    public async Task<int> CountAsync(long? from, long? to, CancellationToken ct = default)
    {
        var query = _context.TherapySettings.AsNoTracking().AsQueryable();
        if (from.HasValue)
            query = query.Where(e => e.Mills >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Mills <= to.Value);
        return await query.CountAsync(ct);
    }

    public async Task<IEnumerable<TherapySettings>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    )
    {
        var entities = await _context
            .TherapySettings.AsNoTracking()
            .Where(e => e.CorrelationId == correlationId)
            .ToListAsync(ct);
        return entities.Select(TherapySettingsMapper.ToDomainModel);
    }

    public async Task<IEnumerable<TherapySettings>> BulkCreateAsync(
        IEnumerable<TherapySettings> records,
        CancellationToken ct = default
    )
    {
        var entities = records.Select(TherapySettingsMapper.ToEntity).ToList();
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
                .TherapySettings.AsNoTracking()
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
            _context.TherapySettings.AddRange(batch);
            await _context.SaveChangesAsync(ct);
            _context.ChangeTracker.Clear();
        }

        return entities.Select(TherapySettingsMapper.ToDomainModel);
    }
}

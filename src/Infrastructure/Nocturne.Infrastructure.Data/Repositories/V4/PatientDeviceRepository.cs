using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class PatientDeviceRepository : IPatientDeviceRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<PatientDeviceRepository> _logger;

    public PatientDeviceRepository(
        NocturneDbContext context,
        ILogger<PatientDeviceRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<PatientDevice>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _context.PatientDevices
            .AsNoTracking()
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return entities.Select(PatientDeviceMapper.ToDomainModel);
    }

    public async Task<IEnumerable<PatientDevice>> GetCurrentAsync(CancellationToken ct = default)
    {
        var entities = await _context.PatientDevices
            .AsNoTracking()
            .Where(e => e.IsCurrent)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return entities.Select(PatientDeviceMapper.ToDomainModel);
    }

    public async Task<IEnumerable<PatientDevice>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken ct = default)
    {
        var fromDate = DateOnly.FromDateTime(from);
        var toDate = DateOnly.FromDateTime(to);

        var entities = await _context.PatientDevices
            .AsNoTracking()
            .Where(e =>
                (e.StartDate == null || e.StartDate <= toDate) &&
                (e.EndDate == null || e.EndDate >= fromDate))
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return entities.Select(PatientDeviceMapper.ToDomainModel);
    }

    public async Task<PatientDevice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.PatientDevices.FindAsync([id], ct);
        return entity is null ? null : PatientDeviceMapper.ToDomainModel(entity);
    }

    public async Task<PatientDevice> CreateAsync(PatientDevice model, CancellationToken ct = default)
    {
        var entity = PatientDeviceMapper.ToEntity(model);
        _context.PatientDevices.Add(entity);
        await _context.SaveChangesAsync(ct);
        return PatientDeviceMapper.ToDomainModel(entity);
    }

    public async Task<PatientDevice> UpdateAsync(Guid id, PatientDevice model, CancellationToken ct = default)
    {
        var entity = await _context.PatientDevices.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"PatientDevice {id} not found");

        PatientDeviceMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return PatientDeviceMapper.ToDomainModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.PatientDevices.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"PatientDevice {id} not found");

        _context.PatientDevices.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }
}

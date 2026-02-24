using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class PumpDeviceRepository : IPumpDeviceRepository
{
    private readonly NocturneDbContext _context;

    public PumpDeviceRepository(NocturneDbContext context)
    {
        _context = context;
    }

    public async Task<PumpDevice?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.PumpDevices.FindAsync([id], ct);
        return entity is null ? null : PumpDeviceMapper.ToDomainModel(entity);
    }

    public async Task<PumpDevice?> FindByTypeAndSerialAsync(string pumpType, string pumpSerial, CancellationToken ct = default)
    {
        var entity = await _context.PumpDevices
            .FirstOrDefaultAsync(e => e.PumpType == pumpType && e.PumpSerial == pumpSerial, ct);
        return entity is null ? null : PumpDeviceMapper.ToDomainModel(entity);
    }

    public async Task<PumpDevice> CreateAsync(PumpDevice model, CancellationToken ct = default)
    {
        var entity = PumpDeviceMapper.ToEntity(model);
        _context.PumpDevices.Add(entity);
        await _context.SaveChangesAsync(ct);
        return PumpDeviceMapper.ToDomainModel(entity);
    }

    public async Task<PumpDevice> UpdateAsync(Guid id, PumpDevice model, CancellationToken ct = default)
    {
        var entity =
            await _context.PumpDevices.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"PumpDevice {id} not found");
        PumpDeviceMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return PumpDeviceMapper.ToDomainModel(entity);
    }
}

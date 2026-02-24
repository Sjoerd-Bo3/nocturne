using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IPumpDeviceRepository
{
    Task<PumpDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PumpDevice?> FindByTypeAndSerialAsync(string pumpType, string pumpSerial, CancellationToken ct = default);
    Task<PumpDevice> CreateAsync(PumpDevice model, CancellationToken ct = default);
    Task<PumpDevice> UpdateAsync(Guid id, PumpDevice model, CancellationToken ct = default);
}

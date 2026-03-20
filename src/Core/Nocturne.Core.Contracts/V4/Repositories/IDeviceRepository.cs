using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IDeviceRepository
{
    Task<Device?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Device?> FindByCategoryTypeAndSerialAsync(DeviceCategory category, string type, string serial, CancellationToken ct = default);
    Task<Device> CreateAsync(Device model, CancellationToken ct = default);
    Task<Device> UpdateAsync(Guid id, Device model, CancellationToken ct = default);
}

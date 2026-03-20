using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts;

public interface IDeviceService
{
    Task<Guid?> ResolveAsync(DeviceCategory category, string? type, string? serial, long mills, CancellationToken ct = default);
}

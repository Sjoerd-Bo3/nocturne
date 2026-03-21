using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces;

public interface IDevicePublisher
{
    Task<bool> PublishDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        string source,
        CancellationToken cancellationToken = default);
}

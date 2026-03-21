using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.ConnectorPublishing;

internal sealed class DevicePublisher : IDevicePublisher
{
    private readonly IDeviceStatusService _deviceStatusService;
    private readonly ILogger<DevicePublisher> _logger;

    public DevicePublisher(
        IDeviceStatusService deviceStatusService,
        ILogger<DevicePublisher> logger)
    {
        _deviceStatusService = deviceStatusService ?? throw new ArgumentNullException(nameof(deviceStatusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> PublishDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _deviceStatusService.CreateDeviceStatusAsync(
                deviceStatuses,
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish device status for {Source}", source);
            return false;
        }
    }
}

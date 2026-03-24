using Nocturne.Core.Models;

namespace Nocturne.API.Models.Requests.V4;

public class UpsertDeviceEventRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public DeviceEventType EventType { get; set; }
    public string? Notes { get; set; }
    public string? SyncIdentifier { get; set; }
}

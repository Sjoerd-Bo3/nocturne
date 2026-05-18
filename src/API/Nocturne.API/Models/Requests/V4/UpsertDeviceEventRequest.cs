using Nocturne.Core.Models;

namespace Nocturne.API.Models.Requests.V4;

/// <summary>
/// Request body for upserting a device event record (site changes, sensor starts, etc.) via the V4 API.
/// </summary>
/// <seealso cref="Validators.V4.UpsertDeviceEventRequestValidator"/>
/// <seealso cref="Nocturne.API.Controllers.V4.Devices.DeviceEventController"/>
public class UpsertDeviceEventRequest
{
    /// <summary>
    /// When the device event occurred.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// UTC offset in minutes at the time of the event, for local-time display.
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Identifier of the device involved in the event.
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Name of the application that submitted this record.
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Upstream data source identifier.
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// The type of device event (e.g. site change, sensor start, pump resume).
    /// </summary>
    public DeviceEventType EventType { get; set; }

    /// <summary>
    /// Free-text notes associated with the event (capped at 10,000 characters).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Upstream sync identifier for deduplication.
    /// </summary>
    public string? SyncIdentifier { get; set; }
}

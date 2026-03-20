namespace Nocturne.Core.Models.V4;

/// <summary>
/// Represents a physical device identified by category, type, and serial number
/// </summary>
public class Device
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Device category discriminator (e.g. InsulinPump, CGM, Uploader)
    /// </summary>
    public DeviceCategory Category { get; set; }

    /// <summary>
    /// Device type/model name (e.g. "Omnipod DASH", "Medtronic 780G")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Device serial number
    /// </summary>
    public string Serial { get; set; } = string.Empty;

    /// <summary>
    /// When this device was first seen as UTC DateTime
    /// </summary>
    public DateTime FirstSeenTimestamp { get; set; }

    /// <summary>
    /// When this device was last seen as UTC DateTime
    /// </summary>
    public DateTime LastSeenTimestamp { get; set; }

    /// <summary>
    /// When this device was first seen in Unix milliseconds (computed)
    /// </summary>
    public long FirstSeenMills => new DateTimeOffset(FirstSeenTimestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// When this device was last seen in Unix milliseconds (computed)
    /// </summary>
    public long LastSeenMills => new DateTimeOffset(LastSeenTimestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

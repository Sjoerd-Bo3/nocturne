namespace Nocturne.Core.Models.V4;

/// <summary>
/// Represents a physical insulin pump identified by type and serial number
/// </summary>
public class PumpDevice
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pump type/model name (e.g. "Omnipod DASH", "Medtronic 780G")
    /// </summary>
    public string PumpType { get; set; } = string.Empty;

    /// <summary>
    /// Pump serial number
    /// </summary>
    public string PumpSerial { get; set; } = string.Empty;

    /// <summary>
    /// When this pump was first seen as UTC DateTime
    /// </summary>
    public DateTime FirstSeenTimestamp { get; set; }

    /// <summary>
    /// When this pump was last seen as UTC DateTime
    /// </summary>
    public DateTime LastSeenTimestamp { get; set; }

    /// <summary>
    /// When this pump was first seen in Unix milliseconds (computed)
    /// </summary>
    public long FirstSeenMills => new DateTimeOffset(FirstSeenTimestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// When this pump was last seen in Unix milliseconds (computed)
    /// </summary>
    public long LastSeenMills => new DateTimeOffset(LastSeenTimestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

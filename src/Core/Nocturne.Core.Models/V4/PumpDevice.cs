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
    /// When this pump was first seen in Unix milliseconds
    /// </summary>
    public long FirstSeenMills { get; set; }

    /// <summary>
    /// When this pump was last seen in Unix milliseconds
    /// </summary>
    public long LastSeenMills { get; set; }
}

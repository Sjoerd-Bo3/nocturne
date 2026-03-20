namespace Nocturne.Core.Models.V4;

/// <summary>
/// Normalized pump status snapshot extracted from DeviceStatus.
/// Fully typed - no JSONB blobs needed.
/// </summary>
public class PumpSnapshot : IV4Record
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public long Mills => new DateTimeOffset(Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public Guid? CorrelationId { get; set; }
    public string? LegacyId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }

    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public double? Reservoir { get; set; }
    public string? ReservoirDisplay { get; set; }
    public int? BatteryPercent { get; set; }
    public double? BatteryVoltage { get; set; }
    public bool? Bolusing { get; set; }
    public bool? Suspended { get; set; }
    public string? PumpStatus { get; set; }
    public string? Clock { get; set; }

    /// <summary>
    /// Foreign key to the Device table
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

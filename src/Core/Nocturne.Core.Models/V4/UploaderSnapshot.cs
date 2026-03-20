namespace Nocturne.Core.Models.V4;

/// <summary>
/// Normalized uploader/phone status snapshot extracted from DeviceStatus.
/// Fully typed - no JSONB blobs needed.
/// </summary>
public class UploaderSnapshot : IV4Record
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

    public string? Name { get; set; }
    public int? Battery { get; set; }
    public double? BatteryVoltage { get; set; }
    public bool? IsCharging { get; set; }
    public double? Temperature { get; set; }
    public string? Type { get; set; }

    /// <summary>
    /// Foreign key to the Device table
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Insulin bolus delivery record
/// </summary>
public class Bolus : IV4Record
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Canonical timestamp as UTC DateTime
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Unix milliseconds (computed from Timestamp for v1/v3 compatibility)
    /// </summary>
    public long Mills => new DateTimeOffset(Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// UTC offset in minutes
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Device identifier that delivered this bolus
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this bolus
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Origin data source identifier
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Links records that were split from the same legacy Treatment
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// Original v1/v3 record ID for migration traceability
    /// </summary>
    public string? LegacyId { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this record was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Insulin units delivered
    /// </summary>
    public double Insulin { get; set; }

    /// <summary>
    /// Original programmed dose before any interruption
    /// </summary>
    public double? Programmed { get; set; }

    /// <summary>
    /// Actual insulin delivered, if different from programmed
    /// </summary>
    public double? Delivered { get; set; }

    /// <summary>
    /// Type of bolus delivery (Normal, Square, Dual)
    /// </summary>
    public BolusType? BolusType { get; set; }

    /// <summary>
    /// Whether this bolus was auto-delivered by an APS system
    /// </summary>
    public bool Automatic { get; set; }

    /// <summary>
    /// How this bolus was initiated (Manual or Algorithm/SMB)
    /// </summary>
    public BolusKind Kind { get; set; } = BolusKind.Manual;

    /// <summary>
    /// Duration in minutes for extended/square boluses
    /// </summary>
    public double? Duration { get; set; }

    /// <summary>
    /// APS system sync/deduplication identifier (used by Loop and AAPS)
    /// </summary>
    public string? SyncIdentifier { get; set; }

    /// <summary>
    /// Insulin type name (e.g. "Humalog", "Novolog")
    /// </summary>
    public string? InsulinType { get; set; }

    /// <summary>
    /// Unabsorbed insulin from previous boluses at time of delivery
    /// </summary>
    public double? Unabsorbed { get; set; }

    /// <summary>
    /// Foreign key to the Device table
    /// </summary>
    public Guid? DeviceId { get; set; }

    /// <summary>
    /// Per-record pump counter (AAPS internal identifier)
    /// </summary>
    public string? PumpRecordId { get; set; }

    /// <summary>
    /// FK to the BolusCalculation that produced this bolus (null for manual/correction/SMB boluses)
    /// </summary>
    public Guid? BolusCalculationId { get; set; }

    /// <summary>
    /// FK to the ApsSnapshot whose algorithm decision triggered this bolus (for SMBs/auto-boluses)
    /// </summary>
    public Guid? ApsSnapshotId { get; set; }

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

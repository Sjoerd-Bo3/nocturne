namespace Nocturne.Core.Models.V4;

/// <summary>
/// Temporary basal rate change record — a time-ranged event
/// representing a deviation from the scheduled basal rate
/// </summary>
public class TempBasal
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Start timestamp in Unix milliseconds
    /// </summary>
    public long StartMills { get; set; }

    /// <summary>
    /// End timestamp in Unix milliseconds (null if still active)
    /// </summary>
    public long? EndMills { get; set; }

    /// <summary>
    /// UTC offset in minutes
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Device identifier that set this temp basal
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this record
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
    /// Temporary basal rate in units per hour
    /// </summary>
    public double Rate { get; set; }

    /// <summary>
    /// Scheduled basal rate that this temp basal overrides
    /// </summary>
    public double? ScheduledRate { get; set; }

    /// <summary>
    /// Origin of this temp basal (Algorithm, Scheduled, Manual, Suspended, Inferred)
    /// </summary>
    public TempBasalOrigin Origin { get; set; }

    /// <summary>
    /// Reference to the pump device that delivered this temp basal
    /// </summary>
    public Guid? PumpDeviceId { get; set; }

    /// <summary>
    /// Pump-specific record identifier for deduplication
    /// </summary>
    public string? PumpRecordId { get; set; }

    /// <summary>
    /// FK to the ApsSnapshot whose algorithm decision set this temp basal
    /// </summary>
    public Guid? ApsSnapshotId { get; set; }
}

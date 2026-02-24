namespace Nocturne.Core.Models.V4;

/// <summary>
/// Algorithm-delivered micro-dose of insulin (Super Micro Bolus / SMB).
/// Extracted from Bolus records where IsBasalInsulin was true.
/// </summary>
public class MicroBolus : IV4Record
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Canonical timestamp in Unix milliseconds
    /// </summary>
    public long Mills { get; set; }

    /// <summary>
    /// UTC offset in minutes
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Device identifier that delivered this micro bolus
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this micro bolus
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
    /// APS system sync/deduplication identifier (used by AAPS)
    /// </summary>
    public string? SyncIdentifier { get; set; }

    /// <summary>
    /// Reference to the pump device that delivered this SMB
    /// </summary>
    public Guid? PumpDeviceId { get; set; }

    /// <summary>
    /// Pump-internal record identifier for this SMB delivery
    /// </summary>
    public string? PumpRecordId { get; set; }
}

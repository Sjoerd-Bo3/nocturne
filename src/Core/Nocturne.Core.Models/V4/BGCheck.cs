namespace Nocturne.Core.Models.V4;

/// <summary>
/// Blood glucose check record (finger stick or sensor check)
/// </summary>
public class BGCheck : IV4Record
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
    /// Device identifier that performed this check
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this check
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
    /// Glucose value as entered by the user (source of truth)
    /// </summary>
    public double Glucose { get; set; }

    /// <summary>
    /// Source type of the glucose reading (Finger, Sensor)
    /// </summary>
    public GlucoseType? GlucoseType { get; set; }

    /// <summary>
    /// Unit of measurement for the Glucose value (source of truth)
    /// </summary>
    public GlucoseUnit? Units { get; set; }

    /// <summary>
    /// Glucose in mg/dL (computed from Glucose + Units)
    /// </summary>
    public double Mgdl => Units == GlucoseUnit.Mmol ? Glucose * 18.0182 : Glucose;

    /// <summary>
    /// Glucose in mmol/L (computed from Glucose + Units)
    /// </summary>
    public double Mmol => Units == GlucoseUnit.Mmol ? Glucose : Glucose / 18.0182;

    /// <summary>
    /// APS system sync/deduplication identifier (used by Loop and AAPS)
    /// </summary>
    public string? SyncIdentifier { get; set; }

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

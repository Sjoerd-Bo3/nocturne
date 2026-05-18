namespace Nocturne.Core.Models.V4;

/// <summary>
/// CGM sensor calibration record containing slope, intercept, and scale values.
/// </summary>
/// <remarks>
/// Calibrations are extracted from legacy <see cref="Entry"/> records that carried calibration
/// data (typically from older CGM systems like Dexcom G4/G5 via xDrip). Modern factory-calibrated
/// CGMs (G6, G7, Libre) generally do not produce calibration records.
/// </remarks>
/// <seealso cref="Entry"/>
/// <seealso cref="IV4Record"/>
/// <seealso cref="SensorGlucose"/>
public class Calibration : IV4Record
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
    /// Device identifier that produced this calibration
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this calibration
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
    /// Calibration slope value
    /// </summary>
    public double? Slope { get; set; }

    /// <summary>
    /// Calibration intercept value
    /// </summary>
    public double? Intercept { get; set; }

    /// <summary>
    /// Calibration scale value
    /// </summary>
    public double? Scale { get; set; }

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

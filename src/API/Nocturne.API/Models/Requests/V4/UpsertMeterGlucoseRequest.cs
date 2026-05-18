namespace Nocturne.API.Models.Requests.V4;

/// <summary>
/// Request body for upserting a meter glucose reading via the V4 API.
/// </summary>
/// <seealso cref="Validators.V4.UpsertMeterGlucoseRequestValidator"/>
/// <seealso cref="Nocturne.API.Controllers.V4.Glucose.MeterGlucoseController"/>
public class UpsertMeterGlucoseRequest
{
    /// <summary>
    /// When the meter reading was taken.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// UTC offset in minutes at the time of the event, for local-time display.
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Identifier of the glucose meter device.
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Name of the application that submitted this record.
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Upstream data source identifier.
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Glucose reading in mg/dL (validated 0-10,000).
    /// </summary>
    public double Mgdl { get; set; }
}

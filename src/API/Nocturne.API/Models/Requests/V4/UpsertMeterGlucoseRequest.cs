namespace Nocturne.API.Models.Requests.V4;

public class UpsertMeterGlucoseRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double Mgdl { get; set; }
}

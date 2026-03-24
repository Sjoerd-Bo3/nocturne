namespace Nocturne.API.Models.Requests.V4;

public class UpsertCalibrationRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double? Slope { get; set; }
    public double? Intercept { get; set; }
    public double? Scale { get; set; }
}

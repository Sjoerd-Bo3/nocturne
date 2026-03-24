using Nocturne.Core.Models.V4;

namespace Nocturne.API.Models.Requests.V4;

public class UpsertSensorGlucoseRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double Mgdl { get; set; }
    public GlucoseDirection? Direction { get; set; }
    public double? TrendRate { get; set; }
    public int? Noise { get; set; }
}

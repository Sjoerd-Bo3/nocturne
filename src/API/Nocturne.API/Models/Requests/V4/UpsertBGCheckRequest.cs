using Nocturne.Core.Models.V4;

namespace Nocturne.API.Models.Requests.V4;

public class UpsertBGCheckRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double Glucose { get; set; }
    public GlucoseUnit? Units { get; set; }
    public GlucoseType? GlucoseType { get; set; }
    public string? SyncIdentifier { get; set; }
}

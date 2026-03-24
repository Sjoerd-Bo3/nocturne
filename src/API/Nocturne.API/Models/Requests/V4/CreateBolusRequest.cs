using Nocturne.Core.Models.V4;

namespace Nocturne.API.Models.Requests.V4;

public class CreateBolusRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double Insulin { get; set; }
    public double? Programmed { get; set; }
    public double? Delivered { get; set; }
    public BolusType? BolusType { get; set; }
    public BolusKind Kind { get; set; } = BolusKind.Manual;
    public bool Automatic { get; set; }
    public double? Duration { get; set; }
    public string? SyncIdentifier { get; set; }
    public string? InsulinType { get; set; }
    public double? Unabsorbed { get; set; }
    public Guid? BolusCalculationId { get; set; }
    public Guid? ApsSnapshotId { get; set; }
}

using Nocturne.Core.Models.V4;

namespace Nocturne.API.Models.Requests.V4;

public class UpsertBolusCalculationRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public double? BloodGlucoseInput { get; set; }
    public string? BloodGlucoseInputSource { get; set; }
    public double? CarbInput { get; set; }
    public double? InsulinOnBoard { get; set; }
    public double? InsulinRecommendation { get; set; }
    public double? CarbRatio { get; set; }
    public CalculationType? CalculationType { get; set; }
    public double? InsulinRecommendationForCarbs { get; set; }
    public double? InsulinProgrammed { get; set; }
    public double? EnteredInsulin { get; set; }
    public double? SplitNow { get; set; }
    public double? SplitExt { get; set; }
    public double? PreBolus { get; set; }
}

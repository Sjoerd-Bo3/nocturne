namespace Nocturne.Core.Models.V4;

/// <summary>
/// Immutable snapshot of insulin properties at treatment delivery time.
/// </summary>
public record TreatmentInsulinContext
{
    public Guid PatientInsulinId { get; init; }
    public string InsulinName { get; init; } = string.Empty;
    public double Dia { get; init; }
    public int Peak { get; init; }
    public string Curve { get; init; } = "rapid-acting";
    public int Concentration { get; init; } = 100;
}

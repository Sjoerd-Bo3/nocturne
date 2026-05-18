namespace Nocturne.Core.Models.V4;

/// <summary>
/// Immutable snapshot of insulin pharmacokinetic properties captured at the time a treatment
/// (<see cref="Bolus"/> or <see cref="CarbIntake"/>) was recorded.
/// </summary>
/// <remarks>
/// Preserving these values at record time ensures that historical IOB calculations remain
/// consistent even after a patient's <see cref="PatientInsulin"/> settings are later changed.
/// </remarks>
/// <seealso cref="PatientInsulin"/>
/// <seealso cref="Bolus"/>
public record TreatmentInsulinContext
{
    /// <summary>
    /// Foreign key to the <see cref="PatientInsulin"/> record that was active at delivery time.
    /// </summary>
    public Guid PatientInsulinId { get; init; }

    /// <summary>
    /// Display name of the insulin at delivery time (snapshot copy).
    /// </summary>
    public string InsulinName { get; init; } = string.Empty;

    /// <summary>
    /// Duration of Insulin Action in hours at delivery time.
    /// </summary>
    public double Dia { get; init; }

    /// <summary>
    /// Time to peak activity in minutes at delivery time.
    /// </summary>
    public int Peak { get; init; }

    /// <summary>
    /// Activity curve model name at delivery time (e.g., "rapid-acting", "ultra-rapid", "bilinear").
    /// </summary>
    public string Curve { get; init; } = "rapid-acting";

    /// <summary>
    /// Insulin concentration in units per mL at delivery time (e.g., 100, 200).
    /// </summary>
    public int Concentration { get; init; } = 100;
}

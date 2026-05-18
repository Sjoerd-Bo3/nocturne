namespace Nocturne.Core.Models.V4;

/// <summary>
/// An insulin formulation prescribed to a patient, with patient-specific pharmacokinetic overrides.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PatientInsulin"/> records which insulin(s) a patient uses, their usage period, and
/// the pharmacokinetic parameters (DIA, peak, curve, concentration) that APS algorithms should use
/// for IOB calculations. These parameters default to <see cref="InsulinCatalog"/> values but can be
/// overridden per patient.
/// </para>
/// <para>
/// When <see cref="FormulationId"/> is set, the record is linked to a known <see cref="InsulinFormulation"/>
/// from <see cref="InsulinCatalog"/>. Custom insulins have a null <see cref="FormulationId"/>.
/// </para>
/// </remarks>
/// <seealso cref="InsulinFormulation"/>
/// <seealso cref="InsulinCategory"/>
/// <seealso cref="InsulinRole"/>
/// <seealso cref="InsulinCatalog"/>
/// <seealso cref="PatientRecord"/>
public class PatientInsulin
{
    /// <summary>
    /// UUID v7 primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Pharmacokinetic category of this insulin (e.g., <see cref="InsulinCategory.RapidActing"/>).
    /// </summary>
    /// <seealso cref="V4.InsulinCategory"/>
    public InsulinCategory InsulinCategory { get; set; }

    /// <summary>
    /// Display name for this insulin (e.g., "Humalog", "Custom Rapid").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Date the patient started using this insulin (inclusive).
    /// </summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Date the patient stopped using this insulin (inclusive), or null if still in use.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Whether this is the patient's currently active insulin for its <see cref="Role"/>.
    /// </summary>
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Optional free-text notes (e.g., dosing instructions, reason for change).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Identifier in the <see cref="InsulinCatalog"/> (e.g., "humalog", "fiasp").
    /// Null for custom insulins not present in the catalog.
    /// </summary>
    public string? FormulationId { get; set; }

    /// <summary>
    /// Duration of Insulin Action in hours (default 4.0).
    /// Overrides the <see cref="InsulinFormulation.DefaultDia"/> from the catalog when changed.
    /// </summary>
    public double Dia { get; set; } = 4.0;

    /// <summary>
    /// Peak activity time in minutes (default 75).
    /// Overrides the <see cref="InsulinFormulation.DefaultPeak"/> from the catalog when changed.
    /// </summary>
    public int Peak { get; set; } = 75;

    /// <summary>
    /// Activity curve model name used by APS algorithms (e.g., "rapid-acting", "ultra-rapid", "bilinear").
    /// </summary>
    public string Curve { get; set; } = "rapid-acting";

    /// <summary>
    /// Insulin concentration in units per mL (e.g., 100, 200, 300, 500).
    /// </summary>
    public int Concentration { get; set; } = 100;

    /// <summary>
    /// Delivery role: bolus only, basal only, or both.
    /// </summary>
    /// <seealso cref="V4.InsulinRole"/>
    public InsulinRole Role { get; set; } = InsulinRole.Both;

    /// <summary>
    /// Whether this is the patient's primary insulin for its <see cref="Role"/> (used by default in calculator suggestions).
    /// </summary>
    public bool IsPrimary { get; set; }

    /// <summary>
    /// When this record was first created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this record was last modified (UTC).
    /// </summary>
    public DateTime ModifiedAt { get; set; }
}

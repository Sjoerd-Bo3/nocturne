namespace Nocturne.Core.Models.V4;

/// <summary>
/// A known insulin formulation with default pharmacokinetic properties used by APS algorithms.
/// Entries are sourced from <see cref="InsulinCatalog"/> and can be overridden per patient via <see cref="PatientInsulin"/>.
/// </summary>
/// <seealso cref="InsulinCatalog"/>
/// <seealso cref="InsulinCategory"/>
/// <seealso cref="PatientInsulin"/>
public record InsulinFormulation
{
    /// <summary>
    /// Unique kebab-case identifier (e.g., "humalog", "fiasp", "lantus").
    /// Used as the <see cref="PatientInsulin.FormulationId"/> reference.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable display name including the generic name (e.g., "Humalog (Insulin Lispro)").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Pharmacokinetic category of this formulation.
    /// </summary>
    /// <seealso cref="V4.InsulinCategory"/>
    public required InsulinCategory Category { get; init; }

    /// <summary>
    /// Default Duration of Insulin Action in hours (e.g., 4.0 for rapid-acting, 24.0 for Lantus).
    /// </summary>
    public required double DefaultDia { get; init; }

    /// <summary>
    /// Default time to peak activity in minutes (e.g., 75 for rapid-acting, 480 for Lantus).
    /// </summary>
    public required int DefaultPeak { get; init; }

    /// <summary>
    /// Activity curve model name used by APS algorithms (e.g., "rapid-acting", "ultra-rapid", "bilinear").
    /// </summary>
    public required string Curve { get; init; }

    /// <summary>
    /// Insulin concentration in units per mL (e.g., 100, 200, 300, 500).
    /// Used to convert between volume and dose.
    /// </summary>
    public required int Concentration { get; init; }
}

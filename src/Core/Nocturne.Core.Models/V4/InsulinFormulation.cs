namespace Nocturne.Core.Models.V4;

/// <summary>
/// A known insulin formulation with default pharmacokinetic properties.
/// </summary>
public record InsulinFormulation
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required InsulinCategory Category { get; init; }
    public required double DefaultDia { get; init; }
    public required int DefaultPeak { get; init; }
    public required string Curve { get; init; }
    public required int Concentration { get; init; }
}

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Value object encapsulating query parameters for treatment reads.
/// </summary>
public sealed record TreatmentQuery
{
    public string? Find { get; init; }
    public int Count { get; init; } = 10;
    public int Skip { get; init; } = 0;
    public bool ReverseResults { get; init; } = false;
}

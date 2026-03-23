namespace Nocturne.Core.Contracts.Entries;

/// <summary>
/// Value object encapsulating query parameters for entry reads.
/// </summary>
public sealed record EntryQuery
{
    public string? Find { get; init; }
    public string? Type { get; init; }
    public int Count { get; init; } = 10;
    public int Skip { get; init; } = 0;
    public string? DateString { get; init; }
    public bool ReverseResults { get; init; } = false;
}

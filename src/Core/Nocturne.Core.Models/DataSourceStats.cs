namespace Nocturne.Core.Models;

/// <summary>
/// Statistics for a data source's data in the database, with per-type breakdowns.
/// Aggregates counts across <see cref="Entry"/>, <see cref="Treatment"/>, and <see cref="StateSpan"/> tables.
/// </summary>
/// <seealso cref="Entry"/>
/// <seealso cref="Treatment"/>
/// <seealso cref="StateSpan"/>
public record DataSourceStats(
    string DataSource,
    long TotalEntries,
    int EntriesLast24Hours,
    DateTime? LastEntryTime,
    DateTime? FirstEntryTime,
    long TotalTreatments,
    int TreatmentsLast24Hours,
    DateTime? LastTreatmentTime,
    DateTime? FirstTreatmentTime,
    long TotalStateSpans,
    int StateSpansLast24Hours,
    DateTime? LastStateSpanTime,
    DateTime? FirstStateSpanTime,
    Dictionary<string, long> TypeBreakdown,
    Dictionary<string, int> TypeBreakdownLast24Hours
)
{
    /// <summary>
    /// Total items across all tables (legacy + V4) from this data source.
    /// Uses TypeBreakdown which already combines legacy and V4 counts.
    /// </summary>
    public long TotalItems => TypeBreakdown.Count > 0
        ? TypeBreakdown.Values.Sum()
        : TotalEntries + TotalTreatments + TotalStateSpans;

    /// <summary>
    /// Total items in the last 24 hours across all tables (legacy + V4)
    /// </summary>
    public int ItemsLast24Hours => TypeBreakdownLast24Hours.Count > 0
        ? TypeBreakdownLast24Hours.Values.Sum()
        : EntriesLast24Hours + TreatmentsLast24Hours + StateSpansLast24Hours;

    /// <summary>
    /// Most recent item time (entry, treatment, or state span)
    /// </summary>
    public DateTime? LastItemTime
    {
        get
        {
            var times = new[] { LastEntryTime, LastTreatmentTime, LastStateSpanTime }
                .Where(t => t.HasValue)
                .ToArray();

            return times.Length > 0 ? times.Max() : null;
        }
    }
};

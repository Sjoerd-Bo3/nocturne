using System.Text.Json;
using Nocturne.Core.Constants;

namespace Nocturne.Core.Models.Entries;

/// <summary>
/// Pure domain logic for entry operations. All methods are static with zero I/O,
/// making them trivially testable without mocks.
/// </summary>
public static class EntryDomainLogic
{
    /// <summary>
    /// Merges legacy entries with V4-projected entries.
    /// Deduplicates by ID and Mills (timestamp).
    /// Orders by Mills descending, applies skip/take.
    /// </summary>
    public static IReadOnlyList<Entry> MergeAndDeduplicate(
        IEnumerable<Entry> legacyEntries,
        IEnumerable<Entry> projectedEntries,
        int count,
        int skip)
    {
        var legacyList = legacyEntries.ToList();
        var legacyIds = legacyList.Select(e => e.Id).Where(id => id != null).ToHashSet();
        var legacyMillsSet = legacyList.Select(e => e.Mills).ToHashSet();

        var filteredProjected = projectedEntries
            .Where(p => !legacyIds.Contains(p.Id) && !legacyMillsSet.Contains(p.Mills));

        return legacyList
            .Concat(filteredProjected)
            .OrderByDescending(e => e.Mills)
            .Skip(skip)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Builds a MongoDB-style JSON find query with data_source filter injected
    /// based on whether demo mode is enabled.
    /// </summary>
    /// <param name="demoEnabled">True to filter FOR demo data, false to filter it OUT.</param>
    /// <param name="existingQuery">Optional existing JSON query to merge with.</param>
    /// <returns>A JSON find query string with the data_source filter.</returns>
    public static string BuildDemoModeFilterQuery(bool demoEnabled, string? existingQuery)
    {
        string demoFilter;
        if (demoEnabled)
        {
            demoFilter = $"\"data_source\":\"{DataSources.DemoService}\"";
        }
        else
        {
            demoFilter = $"\"data_source\":{{\"$ne\":\"{DataSources.DemoService}\"}}";
        }

        if (string.IsNullOrWhiteSpace(existingQuery) || existingQuery == "{}")
        {
            return "{" + demoFilter + "}";
        }

        var trimmed = existingQuery.Trim();
        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            var inner = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (string.IsNullOrEmpty(inner))
            {
                return "{" + demoFilter + "}";
            }
            return "{" + demoFilter + "," + inner + "}";
        }

        // If query doesn't look like JSON, just return demo filter
        return "{" + demoFilter + "}";
    }

    /// <summary>
    /// Parses $gte/$lte time range values from a MongoDB-style JSON find query.
    /// Walks the document looking for numeric $gte / $lte values on any field.
    /// Returns (null, null) if the query is absent or contains no time constraints.
    /// </summary>
    public static (long? From, long? To) ParseTimeRangeFromFind(string? find)
    {
        if (string.IsNullOrEmpty(find))
            return (null, null);

        long? from = null;
        long? to = null;

        try
        {
            using var doc = JsonDocument.Parse(find);
            foreach (var field in doc.RootElement.EnumerateObject())
            {
                if (field.Value.ValueKind != JsonValueKind.Object)
                    continue;

                foreach (var op in field.Value.EnumerateObject())
                {
                    if (op.Value.ValueKind != JsonValueKind.Number)
                        continue;

                    if (op.Name == "$gte" && op.Value.TryGetInt64(out var gte))
                        from = gte;
                    else if (op.Name == "$lte" && op.Value.TryGetInt64(out var lte))
                        to = lte;
                }
            }
        }
        catch (JsonException)
        {
            // Malformed query — return no time bounds, which is safe.
        }

        return (from, to);
    }

    /// <summary>
    /// Returns true for common entry counts that are worth caching (10, 50, 100).
    /// </summary>
    public static bool IsCommonEntryCount(int count) => count is 10 or 50 or 100;

    /// <summary>
    /// Returns the entry with the higher Mills timestamp, handling nulls.
    /// </summary>
    public static Entry? SelectMostRecent(Entry? legacy, Entry? projected)
    {
        if (legacy == null && projected == null)
            return null;
        if (legacy == null)
            return projected;
        if (projected == null)
            return legacy;

        return projected.Mills > legacy.Mills ? projected : legacy;
    }

    /// <summary>
    /// Returns true if the type is null, empty, or "sgv" — i.e., should be projected from V4.
    /// </summary>
    public static bool ShouldProject(string? type) => string.IsNullOrEmpty(type) || type == "sgv";
}

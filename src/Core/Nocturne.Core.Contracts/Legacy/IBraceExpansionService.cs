using System.Text.RegularExpressions;

namespace Nocturne.Core.Contracts.Legacy;

/// <summary>
/// Service for handling bash-style brace expansion for time pattern matching
/// Provides 1:1 compatibility with the legacy JavaScript braces.expand() functionality
/// </summary>
public interface IBraceExpansionService
{
    /// <summary>
    /// Expand a bash-style brace pattern into all matching strings.
    /// </summary>
    /// <remarks>
    /// Supports numeric ranges (e.g., <c>{01..12}</c>), comma lists (e.g., <c>{a,b,c}</c>),
    /// and nested patterns.
    /// </remarks>
    /// <param name="pattern">Brace pattern to expand (e.g., <c>"20{14..16}-{01..12}"</c>).</param>
    /// <returns>All expanded string permutations.</returns>
    IEnumerable<string> ExpandBraces(string pattern);

    /// <summary>
    /// Convert expanded string patterns into compiled <see cref="Regex"/> objects.
    /// </summary>
    /// <param name="patterns">Expanded string patterns.</param>
    /// <param name="prefix">Optional prefix to prepend to each pattern.</param>
    /// <param name="suffix">Optional suffix to append to each pattern.</param>
    /// <returns>Compiled <see cref="Regex"/> objects for matching.</returns>
    IEnumerable<Regex> PatternsToRegex(
        IEnumerable<string> patterns,
        string? prefix = null,
        string? suffix = null
    );

    /// <summary>
    /// Prepare a <see cref="TimePatternQuery"/> from prefix and regex parameters for database querying.
    /// </summary>
    /// <param name="prefix">Time prefix pattern (e.g., <c>"2015-04"</c>).</param>
    /// <param name="regex">Time regex pattern (e.g., <c>"T{13..18}:{00..15}"</c>).</param>
    /// <param name="fieldName">Database field name to match against (default <c>"dateString"</c>).</param>
    /// <returns>A <see cref="TimePatternQuery"/> ready for database execution.</returns>
    TimePatternQuery PrepareTimePatterns(
        string? prefix,
        string? regex,
        string fieldName = "dateString"
    );
}

/// <summary>
/// Result of time pattern preparation for queries
/// </summary>
public class TimePatternQuery
{
    public IEnumerable<string> Patterns { get; set; } = Array.Empty<string>();
    public string FieldName { get; set; } = string.Empty;
    public IEnumerable<string> InPatterns { get; set; } = Array.Empty<string>();
    public string? SingleRegexPattern { get; set; }
    public bool CanOptimizeWithIndex { get; set; }
}

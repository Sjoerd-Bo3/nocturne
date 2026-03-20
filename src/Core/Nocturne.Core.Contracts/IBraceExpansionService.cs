using System.Text.RegularExpressions;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for handling bash-style brace expansion for time pattern matching
/// Provides 1:1 compatibility with the legacy JavaScript braces.expand() functionality
/// </summary>
public interface IBraceExpansionService
{
    IEnumerable<string> ExpandBraces(string pattern);

    IEnumerable<Regex> PatternsToRegex(
        IEnumerable<string> patterns,
        string? prefix = null,
        string? suffix = null
    );

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

namespace Nocturne.Core.Models;

/// <summary>
/// Versions list response model that maintains 1:1 compatibility with Nightscout <c>/api/versions</c> endpoint.
/// Returns the list of supported API versions (e.g., ["v1", "v2", "v3", "v4"]).
/// </summary>
/// <seealso cref="VersionResponse"/>
public class VersionsResponse
{
    /// <summary>
    /// List of supported API versions
    /// </summary>
    public List<string> Versions { get; set; } = new();
}

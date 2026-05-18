using System.Text.Json.Serialization;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// RFC 7591 Dynamic Client Registration request body.
/// </summary>
public class ClientRegistrationRequest
{
    /// <summary>
    /// Human-readable name of the client application.
    /// </summary>
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// List of allowed redirect URIs for the authorization code flow.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// Space-delimited list of scopes the client intends to request.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// URL of the client application's homepage.
    /// </summary>
    [JsonPropertyName("client_uri")]
    public string? ClientUri { get; set; }

    /// <summary>
    /// URL of the client application's logo image.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; set; }

    /// <summary>
    /// Stable identifier for the software package (for deduplication across registrations).
    /// </summary>
    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; set; }

    /// <summary>
    /// Version string of the client software.
    /// </summary>
    [JsonPropertyName("software_version")]
    public string? SoftwareVersion { get; set; }
}

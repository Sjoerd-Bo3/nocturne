using System.Text.Json.Serialization;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// RFC 7591 Dynamic Client Registration response body.
/// </summary>
public class ClientRegistrationResponse
{
    /// <summary>
    /// Assigned OAuth client identifier.
    /// </summary>
    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = "";

    /// <summary>
    /// Unix timestamp (seconds since epoch) when the client ID was issued.
    /// </summary>
    [JsonPropertyName("client_id_issued_at")]
    public long ClientIdIssuedAt { get; set; }

    /// <summary>
    /// Human-readable name echoed from the registration request.
    /// </summary>
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// Registered redirect URIs.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public List<string> RedirectUris { get; set; } = [];

    /// <summary>
    /// OAuth grant types the client is authorized to use.
    /// </summary>
    /// <value>Defaults to authorization_code and refresh_token.</value>
    [JsonPropertyName("grant_types")]
    public List<string> GrantTypes { get; set; } = ["authorization_code", "refresh_token"];

    /// <summary>
    /// OAuth response types the client may request.
    /// </summary>
    /// <value>Defaults to "code".</value>
    [JsonPropertyName("response_types")]
    public List<string> ResponseTypes { get; set; } = ["code"];

    /// <summary>
    /// Authentication method the client uses at the token endpoint.
    /// </summary>
    /// <value>Defaults to "none" (public client, PKCE required).</value>
    [JsonPropertyName("token_endpoint_auth_method")]
    public string TokenEndpointAuthMethod { get; set; } = "none";

    /// <summary>
    /// Space-delimited scopes the client is permitted to request.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Software identifier echoed from the registration request.
    /// </summary>
    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; set; }
}

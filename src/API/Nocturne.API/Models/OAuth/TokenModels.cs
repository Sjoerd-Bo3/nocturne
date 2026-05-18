using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace Nocturne.API.Models.OAuth;

/// <summary>
/// OAuth 2.0 error response (RFC 6749 Section 5.2)
/// </summary>
public class OAuthError
{
    /// <summary>
    /// Error code as defined in RFC 6749 Section 5.2 (e.g. "invalid_grant", "invalid_client").
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the error.
    /// </summary>
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// URI pointing to a page with more information about the error.
    /// </summary>
    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; set; }
}

/// <summary>
/// OAuth 2.0 token request (RFC 6749 Section 4.1.3)
/// </summary>
public class OAuthTokenRequest
{
    /// <summary>
    /// OAuth grant type (e.g. "authorization_code", "refresh_token", "urn:ietf:params:oauth:grant-type:device_code").
    /// </summary>
    [FromForm(Name = "grant_type")]
    public string GrantType { get; set; } = string.Empty;

    /// <summary>
    /// Authorization code received from the authorization endpoint (authorization_code grant).
    /// </summary>
    [FromForm(Name = "code")]
    public string? Code { get; set; }

    /// <summary>
    /// Redirect URI that was used in the original authorization request.
    /// </summary>
    [FromForm(Name = "redirect_uri")]
    public string? RedirectUri { get; set; }

    /// <summary>
    /// OAuth client identifier.
    /// </summary>
    [FromForm(Name = "client_id")]
    public string? ClientId { get; set; }

    /// <summary>
    /// PKCE code verifier corresponding to the code challenge sent in the authorization request.
    /// </summary>
    [FromForm(Name = "code_verifier")]
    public string? CodeVerifier { get; set; }

    /// <summary>
    /// Refresh token for obtaining a new access token (refresh_token grant).
    /// </summary>
    [FromForm(Name = "refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Device code received from the device authorization endpoint (device_code grant).
    /// </summary>
    [FromForm(Name = "device_code")]
    public string? DeviceCode { get; set; }

    /// <summary>
    /// Requested scope (used with refresh_token grant to narrow scopes).
    /// </summary>
    [FromForm(Name = "scope")]
    public string? Scope { get; set; }
}

/// <summary>
/// OAuth 2.0 token response (RFC 6749 Section 5.1)
/// </summary>
public class OAuthTokenResponse
{
    /// <summary>
    /// The issued access token.
    /// </summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token type; always "Bearer".
    /// </summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    /// <summary>
    /// Access token lifetime in seconds.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    /// <summary>
    /// Refresh token that can be used to obtain new access tokens.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Space-delimited scopes granted by this token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

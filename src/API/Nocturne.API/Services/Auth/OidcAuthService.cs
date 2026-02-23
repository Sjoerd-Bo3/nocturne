using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Configuration;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Service for handling OIDC authentication flows
/// </summary>
public class OidcAuthService : IOidcAuthService
{
    private readonly IOidcProviderService _providerService;
    private readonly ISubjectService _subjectService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OidcOptions _options;
    private readonly ILogger<OidcAuthService> _logger;

    /// <summary>
    /// Creates a new instance of OidcAuthService
    /// </summary>
    public OidcAuthService(
        IOidcProviderService providerService,
        ISubjectService subjectService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        IHttpClientFactory httpClientFactory,
        IOptions<OidcOptions> options,
        ILogger<OidcAuthService> logger
    )
    {
        _providerService = providerService;
        _subjectService = subjectService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OidcAuthorizationRequest> GenerateAuthorizationUrlAsync(
        Guid? providerId,
        string? returnUrl = null,
        string? state = null
    )
    {
        OidcProvider provider;

        if (providerId.HasValue)
        {
            provider =
                await _providerService.GetProviderByIdAsync(providerId.Value)
                ?? throw new InvalidOperationException($"OIDC provider {providerId} not found");
        }
        else
        {
            var providers = await _providerService.GetEnabledProvidersAsync();
            provider =
                providers.FirstOrDefault()
                ?? throw new InvalidOperationException("No OIDC providers configured");
        }

        if (!provider.IsEnabled)
        {
            throw new InvalidOperationException($"OIDC provider {provider.Name} is not enabled");
        }

        // Get discovery document for authorization endpoint
        var discoveryDoc =
            await _providerService.GetDiscoveryDocumentAsync(provider.Id)
            ?? throw new InvalidOperationException(
                $"Could not fetch OIDC discovery document for {provider.Name}"
            );

        // Generate state parameter (includes return URL, provider ID, and nonce)
        var stateData = new OidcStateData
        {
            ProviderId = provider.Id,
            ReturnUrl = returnUrl ?? _options.DefaultReturnUrl,
            Nonce = _options.State.IncludeNonce ? GenerateRandomString(32) : null,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.Add(_options.State.Lifetime),
        };

        state ??= EncodeState(stateData);

        // Build authorization URL
        var redirectUri = GetRedirectUri();
        var authUrl = BuildAuthorizationUrl(
            discoveryDoc.AuthorizationEndpoint,
            provider.ClientId,
            redirectUri,
            provider.Scopes,
            state,
            stateData.Nonce
        );

        return new OidcAuthorizationRequest
        {
            AuthorizationUrl = authUrl,
            State = state,
            Nonce = stateData.Nonce,
            ProviderId = provider.Id,
            ReturnUrl = stateData.ReturnUrl,
            ExpiresAt = stateData.ExpiresAt,
        };
    }

    /// <inheritdoc />
    public async Task<OidcCallbackResult> HandleCallbackAsync(
        string code,
        string state,
        string expectedState,
        string? ipAddress = null,
        string? userAgent = null
    )
    {
        // Validate state parameter
        if (string.IsNullOrEmpty(state) || state != expectedState)
        {
            return OidcCallbackResult.Failed(
                "invalid_state",
                "State parameter mismatch - possible CSRF attack"
            );
        }

        // Decode state to get provider ID and return URL
        OidcStateData stateData;
        try
        {
            stateData = DecodeState(state);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decode OIDC state");
            return OidcCallbackResult.Failed("invalid_state", "Invalid state format");
        }

        // Check state expiration
        if (stateData.ExpiresAt < DateTimeOffset.UtcNow)
        {
            return OidcCallbackResult.Failed("expired_state", "Authentication request has expired");
        }

        // Get provider
        var provider = await _providerService.GetProviderByIdAsync(stateData.ProviderId);
        if (provider == null || !provider.IsEnabled)
        {
            return OidcCallbackResult.Failed(
                "invalid_provider",
                "OIDC provider not found or disabled"
            );
        }

        // Get discovery document
        var discoveryDoc = await _providerService.GetDiscoveryDocumentAsync(provider.Id);
        if (discoveryDoc == null)
        {
            return OidcCallbackResult.Failed(
                "provider_error",
                "Could not fetch provider configuration"
            );
        }

        // Exchange code for tokens
        OidcProviderTokenResponse providerTokens;
        try
        {
            providerTokens = await ExchangeCodeForTokensAsync(
                discoveryDoc.TokenEndpoint,
                code,
                provider.ClientId,
                provider.ClientSecret,
                GetRedirectUri()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Token exchange failed");
            return OidcCallbackResult.Failed("token_exchange_failed", ex.Message);
        }

        // Parse and validate ID token
        OidcIdTokenClaims idTokenClaims;
        try
        {
            idTokenClaims = ParseIdToken(providerTokens.IdToken);

            // Verify nonce if it was included
            if (!string.IsNullOrEmpty(stateData.Nonce) && idTokenClaims.Nonce != stateData.Nonce)
            {
                return OidcCallbackResult.Failed("invalid_nonce", "ID token nonce mismatch");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ID token parsing failed");
            return OidcCallbackResult.Failed("invalid_id_token", ex.Message);
        }

        // Find or create subject
        var subject = await _subjectService.FindOrCreateFromOidcAsync(
            idTokenClaims.Sub,
            provider.IssuerUrl,
            idTokenClaims.Email,
            idTokenClaims.Name ?? idTokenClaims.PreferredUsername,
            provider.DefaultRoles
        );

        // Update last login
        await _subjectService.UpdateLastLoginAsync(subject.Id);

        // Get permissions and roles
        var permissions = await _subjectService.GetSubjectPermissionsAsync(subject.Id);
        var roles = await _subjectService.GetSubjectRolesAsync(subject.Id);

        // Generate our session tokens
        var accessTokenLifetime = _options.Session.AccessTokenLifetime;
        var accessToken = _jwtService.GenerateAccessToken(
            new SubjectInfo
            {
                Id = subject.Id,
                Name = subject.Name,
                Email = subject.Email,
                OidcSubjectId = idTokenClaims.Sub,
                OidcIssuer = provider.IssuerUrl,
            },
            permissions,
            roles,
            accessTokenLifetime
        );

        var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
            subject.Id,
            oidcSessionId: idTokenClaims.SessionId,
            deviceDescription: ParseUserAgentShort(userAgent),
            ipAddress: ipAddress,
            userAgent: userAgent
        );

        var tokens = new OidcTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = (int)accessTokenLifetime.TotalSeconds,
            RefreshExpiresIn = (int)_options.Session.RefreshTokenLifetime.TotalSeconds,
            ExpiresAt = DateTimeOffset.UtcNow.Add(accessTokenLifetime),
            SubjectId = subject.Id,
        };

        var userInfo = new OidcUserInfo
        {
            SubjectId = subject.Id,
            Name = subject.Name,
            Email = subject.Email,
            EmailVerified = idTokenClaims.EmailVerified,
            Picture = idTokenClaims.Picture,
            Roles = roles,
            Permissions = permissions,
            ProviderName = provider.Name,
            LastLoginAt = DateTimeOffset.UtcNow,
        };

        _logger.LogInformation(
            "OIDC authentication successful for user {Name} ({Email}) via {Provider}",
            subject.Name,
            subject.Email ?? "no email",
            provider.Name
        );

        return OidcCallbackResult.Succeeded(tokens, userInfo, stateData.ReturnUrl);
    }

    /// <inheritdoc />
    public async Task<OidcTokenResponse?> RefreshSessionAsync(
        string refreshToken,
        string? ipAddress = null,
        string? userAgent = null
    )
    {
        // Validate and rotate the refresh token
        string? newRefreshToken;

        if (_options.Session.RotateRefreshTokens)
        {
            newRefreshToken = await _refreshTokenService.RotateRefreshTokenAsync(
                refreshToken,
                ipAddress,
                userAgent
            );
        }
        else
        {
            var subjectId = await _refreshTokenService.ValidateRefreshTokenAsync(refreshToken);
            if (!subjectId.HasValue)
            {
                return null;
            }

            // Update last used timestamp
            await _refreshTokenService.UpdateLastUsedAsync(refreshToken);
            newRefreshToken = refreshToken;
        }

        if (string.IsNullOrEmpty(newRefreshToken))
        {
            return null;
        }

        // Get subject ID from the new refresh token
        var subjectIdResult = await _refreshTokenService.ValidateRefreshTokenAsync(newRefreshToken);
        if (!subjectIdResult.HasValue)
        {
            return null;
        }

        var subjectId2 = subjectIdResult.Value;

        // Get subject details
        var subject = await _subjectService.GetSubjectByIdAsync(subjectId2);
        if (subject == null || !subject.IsActive)
        {
            // Revoke the token if subject is inactive
            await _refreshTokenService.RevokeRefreshTokenAsync(newRefreshToken, "Subject inactive");
            return null;
        }

        // Get permissions and roles
        var permissions = await _subjectService.GetSubjectPermissionsAsync(subjectId2);
        var roles = await _subjectService.GetSubjectRolesAsync(subjectId2);

        // Generate new access token
        var accessTokenLifetime = _options.Session.AccessTokenLifetime;
        var accessToken = _jwtService.GenerateAccessToken(
            new SubjectInfo
            {
                Id = subject.Id,
                Name = subject.Name,
                Email = subject.Email,
                OidcSubjectId = subject.OidcSubjectId,
                OidcIssuer = subject.OidcIssuer,
            },
            permissions,
            roles,
            accessTokenLifetime
        );

        return new OidcTokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = (int)accessTokenLifetime.TotalSeconds,
            RefreshExpiresIn = (int)_options.Session.RefreshTokenLifetime.TotalSeconds,
            ExpiresAt = DateTimeOffset.UtcNow.Add(accessTokenLifetime),
            SubjectId = subjectId2,
        };
    }

    /// <inheritdoc />
    public async Task<OidcLogoutResult> LogoutAsync(string refreshToken, Guid? providerId = null)
    {
        // Revoke the refresh token
        var revoked = await _refreshTokenService.RevokeRefreshTokenAsync(
            refreshToken,
            "User logout"
        );

        if (!revoked)
        {
            // Token might already be revoked, which is fine
            _logger.LogDebug("Refresh token not found or already revoked during logout");
        }

        // Get provider logout URL if requested
        string? providerLogoutUrl = null;
        if (providerId.HasValue)
        {
            var provider = await _providerService.GetProviderByIdAsync(providerId.Value);
            if (provider != null)
            {
                var discoveryDoc = await _providerService.GetDiscoveryDocumentAsync(
                    providerId.Value
                );
                if (!string.IsNullOrEmpty(discoveryDoc?.EndSessionEndpoint))
                {
                    // Build RP-initiated logout URL
                    var logoutUrl = new UriBuilder(discoveryDoc.EndSessionEndpoint);
                    var query = System.Web.HttpUtility.ParseQueryString(string.Empty);
                    query["client_id"] = provider.ClientId;
                    query["post_logout_redirect_uri"] = _options.BaseUrl ?? "";
                    logoutUrl.Query = query.ToString();
                    providerLogoutUrl = logoutUrl.ToString();
                }
            }
        }

        return OidcLogoutResult.Succeeded(providerLogoutUrl);
    }

    /// <inheritdoc />
    public async Task<OidcUserInfo?> GetUserInfoAsync(Guid subjectId)
    {
        var subject = await _subjectService.GetSubjectByIdAsync(subjectId);
        if (subject == null)
        {
            return null;
        }

        var permissions = await _subjectService.GetSubjectPermissionsAsync(subjectId);
        var roles = await _subjectService.GetSubjectRolesAsync(subjectId);

        // Get provider name if linked
        string? providerName = null;
        if (!string.IsNullOrEmpty(subject.OidcIssuer))
        {
            var provider = await _providerService.GetProviderByIssuerAsync(subject.OidcIssuer);
            providerName = provider?.Name;
        }

        return new OidcUserInfo
        {
            SubjectId = subject.Id,
            Name = subject.Name,
            Email = subject.Email,
            Roles = roles,
            Permissions = permissions,
            ProviderName = providerName,
            LastLoginAt = subject.LastLoginAt,
            PreferredLanguage = subject.PreferredLanguage,
        };
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateSessionAsync(string refreshToken)
    {
        return await _refreshTokenService.ValidateRefreshTokenAsync(refreshToken);
    }

    #region Private Helper Methods

    /// <summary>
    /// Get the redirect URI for OIDC callbacks
    /// </summary>
    private string GetRedirectUri()
    {
        var baseUrl = _options.BaseUrl?.TrimEnd('/') ?? "http://localhost:5000";
        return $"{baseUrl}/auth/callback";
    }

    /// <summary>
    /// Build the authorization URL for the OIDC provider
    /// </summary>
    private static string BuildAuthorizationUrl(
        string authorizationEndpoint,
        string clientId,
        string redirectUri,
        IEnumerable<string> scopes,
        string state,
        string? nonce
    )
    {
        var url = new UriBuilder(authorizationEndpoint);
        var query = System.Web.HttpUtility.ParseQueryString(string.Empty);

        query["response_type"] = "code";
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["scope"] = string.Join(" ", scopes);
        query["state"] = state;

        if (!string.IsNullOrEmpty(nonce))
        {
            query["nonce"] = nonce;
        }

        url.Query = query.ToString();
        return url.ToString();
    }

    /// <summary>
    /// Exchange authorization code for tokens
    /// </summary>
    private async Task<OidcProviderTokenResponse> ExchangeCodeForTokensAsync(
        string tokenEndpoint,
        string code,
        string clientId,
        string? clientSecret,
        string redirectUri
    )
    {
        var httpClient = _httpClientFactory.CreateClient("OidcProvider");

        var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
            }
        );

        // Add client secret if provided (confidential client)
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}")
            );
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Basic",
                credentials
            );
        }

        var response = await httpClient.PostAsync(tokenEndpoint, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Token exchange failed: {StatusCode} - {Response}",
                response.StatusCode,
                responseBody
            );
            throw new InvalidOperationException($"Token exchange failed: {response.StatusCode}");
        }

        var tokens = JsonSerializer.Deserialize<OidcProviderTokenResponse>(
            responseBody,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        return tokens ?? throw new InvalidOperationException("Empty token response");
    }

    /// <summary>
    /// Parse claims from an ID token (without full signature validation -
    /// signature is validated by OidcTokenHandler when token is used)
    /// </summary>
    private static OidcIdTokenClaims ParseIdToken(string idToken)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3)
        {
            throw new InvalidOperationException("Invalid ID token format");
        }

        var payload = parts[1];
        // Pad base64 if needed
        switch (payload.Length % 4)
        {
            case 2:
                payload += "==";
                break;
            case 3:
                payload += "=";
                break;
        }

        var json = Encoding.UTF8.GetString(
            Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/'))
        );

        var claims = JsonSerializer.Deserialize<OidcIdTokenClaims>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        );

        return claims ?? throw new InvalidOperationException("Invalid ID token claims");
    }

    /// <summary>
    /// Generate a cryptographically secure random string
    /// </summary>
    private static string GenerateRandomString(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>
    /// Encode state data as a URL-safe string
    /// </summary>
    private static string EncodeState(OidcStateData data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    /// <summary>
    /// Decode state data from a URL-safe string
    /// </summary>
    private static OidcStateData DecodeState(string encoded)
    {
        // Restore base64 padding
        switch (encoded.Length % 4)
        {
            case 2:
                encoded += "==";
                break;
            case 3:
                encoded += "=";
                break;
        }

        var bytes = Convert.FromBase64String(encoded.Replace("-", "+").Replace("_", "/"));
        var json = Encoding.UTF8.GetString(bytes);

        return JsonSerializer.Deserialize<OidcStateData>(json)
            ?? throw new InvalidOperationException("Invalid state data");
    }

    /// <summary>
    /// Parse a short device description from user agent
    /// </summary>
    private static string? ParseUserAgentShort(string? userAgent)
    {
        if (string.IsNullOrEmpty(userAgent))
            return null;

        // Simple parsing - in production you might use a library like UAParser
        if (userAgent.Contains("Windows"))
            return "Windows";
        if (userAgent.Contains("Macintosh"))
            return "Mac";
        if (userAgent.Contains("Linux"))
            return "Linux";
        if (userAgent.Contains("iPhone"))
            return "iPhone";
        if (userAgent.Contains("iPad"))
            return "iPad";
        if (userAgent.Contains("Android"))
            return "Android";

        return userAgent.Length > 50 ? userAgent[..50] + "..." : userAgent;
    }

    #endregion

    #region Private Classes

    /// <summary>
    /// State data encoded in the state parameter
    /// </summary>
    private class OidcStateData
    {
        public Guid ProviderId { get; set; }
        public string? ReturnUrl { get; set; }
        public string? Nonce { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <summary>
    /// Token response from OIDC provider
    /// </summary>
    private class OidcProviderTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public string IdToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public int ExpiresIn { get; set; }
    }

    /// <summary>
    /// Claims extracted from ID token
    /// </summary>
    private class OidcIdTokenClaims
    {
        public string Sub { get; set; } = string.Empty;
        public string? Email { get; set; }
        public bool? EmailVerified { get; set; }
        public string? Name { get; set; }
        public string? PreferredUsername { get; set; }
        public string? GivenName { get; set; }
        public string? FamilyName { get; set; }
        public string? Picture { get; set; }
        public string? Nonce { get; set; }
        public string? Sid { get; set; } // Session ID
        public string? SessionId => Sid;
        public long? Iat { get; set; }
        public long? Exp { get; set; }
    }

    #endregion
}

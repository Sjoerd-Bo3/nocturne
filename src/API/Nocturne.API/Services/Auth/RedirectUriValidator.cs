namespace Nocturne.API.Services.Auth;

/// <summary>
/// Validates redirect URIs per RFC 8252 (OAuth 2.0 for Native Apps).
/// </summary>
public class RedirectUriValidator
{
    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "127.0.0.1",
        "[::1]",
        "localhost",
    };

    /// <summary>
    /// Validates a redirect URI submitted in a Dynamic Client Registration (DCR) request.
    /// </summary>
    /// <param name="uri">The candidate redirect URI to validate.</param>
    /// <returns>
    /// <see langword="true"/> if the URI is an absolute URI without a fragment and belongs to
    /// a recognised URI class (custom scheme, loopback HTTP, or claimed HTTPS).
    /// </returns>
    public bool IsValidForRegistration(string uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
            return false;

        // Fragments are never allowed (RFC 6749 Section 3.1.2)
        if (uri.Contains('#'))
            return false;

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            return false;

        return Classify(parsed) != UriClass.Invalid;
    }

    /// <summary>
    /// Validates a redirect URI presented during <c>/oauth/authorize</c> against a registered one.
    /// Uses byte-exact matching except for loopback URIs, which allow any port per RFC 8252 Section 7.3.
    /// </summary>
    /// <param name="registered">The redirect URI stored at registration time.</param>
    /// <param name="presented">The redirect URI included in the current authorisation request.</param>
    /// <returns>
    /// <see langword="true"/> if the URIs match (byte-exact for non-loopback, or scheme/host/path match for loopback).
    /// </returns>
    public bool IsValidForAuthorize(string registered, string presented)
    {
        if (string.Equals(registered, presented, StringComparison.Ordinal))
            return true;

        // For loopback, allow port variation (RFC 8252 Section 7.3)
        if (!Uri.TryCreate(registered, UriKind.Absolute, out var regUri) ||
            !Uri.TryCreate(presented, UriKind.Absolute, out var presUri))
            return false;

        if (Classify(regUri) != UriClass.Loopback)
            return false;

        // Scheme, host, and path must match; port may differ
        return string.Equals(regUri.Scheme, presUri.Scheme, StringComparison.OrdinalIgnoreCase)
            && string.Equals(regUri.Host, presUri.Host, StringComparison.OrdinalIgnoreCase)
            && string.Equals(regUri.AbsolutePath, presUri.AbsolutePath, StringComparison.Ordinal);
    }

    private static UriClass Classify(Uri uri)
    {
        // Custom scheme (not http/https) — must contain a dot to prevent hijacking
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return uri.Scheme.Contains('.') ? UriClass.CustomScheme : UriClass.Invalid;
        }

        var isLoopback = LoopbackHosts.Contains(uri.Host);

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            // HTTPS + loopback is rejected (RFC 8252 Section 8.3: use http for loopback)
            return isLoopback ? UriClass.Invalid : UriClass.ClaimedHttps;
        }

        // HTTP — only loopback is allowed
        return isLoopback ? UriClass.Loopback : UriClass.Invalid;
    }

    private enum UriClass
    {
        Invalid,
        CustomScheme,
        ClaimedHttps,
        Loopback,
    }
}

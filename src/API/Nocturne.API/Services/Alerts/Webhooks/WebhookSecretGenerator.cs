using System.Security.Cryptography;

namespace Nocturne.API.Services.Alerts.Webhooks;

/// <summary>
/// Generates cryptographically secure random secrets for use with <see cref="WebhookSignature"/>.
/// </summary>
public static class WebhookSecretGenerator
{
    /// <summary>
    /// Generates a random lowercase hexadecimal secret string.
    /// </summary>
    /// <param name="bytes">Number of random bytes to generate (default: 32, yielding a 64-character hex string).</param>
    /// <returns>A lowercase hexadecimal string of length <c><paramref name="bytes"/> * 2</c>.</returns>
    public static string Generate(int bytes = 32)
    {
        var buffer = new byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}

using System.Security.Cryptography;
using System.Text;

namespace Nocturne.API.Services.Alerts.Webhooks;

/// <summary>
/// Creates HMAC-SHA256 signatures for outgoing webhook requests.
/// </summary>
/// <remarks>
/// The signed data is formatted as <c>{timestamp}.{payload}</c> before hashing,
/// mirroring the Stripe signature scheme. The receiver should re-compute the signature
/// using the shared secret and compare it with the <c>X-Nocturne-Signature</c> header
/// to verify authenticity.
/// </remarks>
public static class WebhookSignature
{
    /// <summary>
    /// Computes an HMAC-SHA256 signature for the given payload and timestamp.
    /// </summary>
    /// <param name="secret">The shared secret used as the HMAC key.</param>
    /// <param name="payload">The raw JSON payload body.</param>
    /// <param name="timestamp">Unix timestamp (seconds) included in the signed data to prevent replay attacks.</param>
    /// <returns>A lowercase hexadecimal representation of the HMAC-SHA256 hash.</returns>
    public static string Create(string secret, string payload, long timestamp)
    {
        var data = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

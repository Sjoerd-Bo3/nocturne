using System.Text.Json;

namespace Nocturne.API.Services.Alerts.Webhooks;

/// <summary>
/// Immutable value object that holds a list of webhook URLs and an optional HMAC signing secret.
/// </summary>
/// <param name="Urls">The list of destination webhook URLs.</param>
/// <param name="Secret">Optional HMAC signing secret used by <see cref="WebhookSignature"/>.</param>
public sealed record WebhookConfiguration(IReadOnlyList<string> Urls, string? Secret);

/// <summary>
/// Parses and serialises <see cref="WebhookConfiguration"/> from JSON stored in the database.
/// </summary>
/// <remarks>
/// Accepts two JSON formats: a structured object with <c>urls</c>/<c>secret</c> properties,
/// or a bare JSON array of URL strings. Falls back to an empty configuration on parse failure.
/// </remarks>
public static class WebhookConfigurationParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parses a <see cref="WebhookConfiguration"/> from a JSON string.
    /// </summary>
    /// <param name="json">
    /// JSON string representing either a structured webhook config object or a plain URL array.
    /// May be <see langword="null"/> or empty, in which case an empty configuration is returned.
    /// </param>
    /// <param name="logger">Optional logger for diagnostic messages on parse failure.</param>
    /// <returns>A <see cref="WebhookConfiguration"/> with normalised URLs and optional secret.</returns>
    public static WebhookConfiguration Parse(string? json, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WebhookConfiguration([], null);
        }

        try
        {
            var payload = JsonSerializer.Deserialize<WebhookConfigurationPayload>(json, JsonOptions);
            if (payload?.Urls != null)
            {
                return new WebhookConfiguration(
                    NormalizeUrls(payload.Urls),
                    NormalizeSecret(payload.Secret)
                );
            }
        }
        catch (Exception ex)
        {
            logger?.LogDebug(ex, "Webhook config payload was not an object");
        }

        try
        {
            var urls = JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
            return new WebhookConfiguration(NormalizeUrls(urls), null);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Webhook URLs payload could not be parsed");
        }

        return new WebhookConfiguration([], null);
    }

    /// <summary>
    /// Serialises a list of webhook URLs and an optional secret to the structured JSON format.
    /// </summary>
    /// <param name="urls">The webhook destination URLs to serialise.</param>
    /// <param name="secret">Optional HMAC signing secret, or <see langword="null"/>.</param>
    /// <returns>A JSON string suitable for storage in the database.</returns>
    public static string Serialize(IReadOnlyCollection<string> urls, string? secret)
    {
        var payload = new WebhookConfigurationPayload
        {
            Urls = NormalizeUrls(urls).ToArray(),
            Secret = NormalizeSecret(secret),
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static IReadOnlyList<string> NormalizeUrls(IEnumerable<string> urls)
    {
        return urls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .Select(url => url.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? NormalizeSecret(string? secret)
    {
        return string.IsNullOrWhiteSpace(secret) ? null : secret.Trim();
    }

    private sealed class WebhookConfigurationPayload
    {
        public string[]? Urls { get; init; }
        public string? Secret { get; init; }
    }
}

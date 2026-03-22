using System.Text.Json;
using Nocturne.API.Services.Alerts.Webhooks;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Providers;

/// <summary>
/// Delivers alert payloads via HTTP POST to configured webhook URLs
/// using the existing WebhookRequestSender infrastructure.
/// </summary>
internal sealed class WebhookProvider(
    WebhookRequestSender webhookSender,
    ILogger<WebhookProvider> logger)
{
    public async Task SendAsync(string destination, AlertPayload payload, CancellationToken ct)
    {
        var payloadJson = JsonSerializer.Serialize(payload);

        // Destination may contain multiple URLs separated by commas
        var urls = destination.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var failures = await webhookSender.SendAsync(urls, payloadJson, secret: null, ct);

        if (failures.Count > 0)
        {
            logger.LogWarning("Webhook delivery failed for {FailCount}/{Total} URLs for instance {InstanceId}",
                failures.Count, urls.Length, payload.InstanceId);
            throw new InvalidOperationException($"Webhook delivery failed for {failures.Count} of {urls.Length} URLs");
        }

        logger.LogDebug("Webhook alert sent to {UrlCount} URLs for instance {InstanceId}",
            urls.Length, payload.InstanceId);
    }
}

using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Providers;

/// <summary>
/// Delivers alert payloads to the tenant's alert subscribers via SignalR (web push).
/// This is the fast-path delivery that clients connected to the AlertHub receive.
/// </summary>
internal sealed class WebPushProvider(
    ISignalRBroadcastService broadcastService,
    ILogger<WebPushProvider> logger)
{
    public async Task SendAsync(AlertPayload payload, CancellationToken ct)
    {
        try
        {
            await broadcastService.BroadcastAlertEventAsync("alert_push", payload);
            logger.LogDebug("Web push alert sent for instance {InstanceId}", payload.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send web push alert for instance {InstanceId}", payload.InstanceId);
            throw;
        }
    }
}

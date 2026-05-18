using Nocturne.Core.Models;
using Nocturne.API.Services.Realtime;

namespace Nocturne.API.Services.Alerts.Providers;

/// <summary>
/// Delivers alert payloads to the tenant's alert subscribers via SignalR (web push).
/// This is the fast-path delivery that clients connected to the AlertHub receive.
/// </summary>
internal sealed class WebPushProvider(
    ISignalRBroadcastService broadcastService,
    ILogger<WebPushProvider> logger)
{
    /// <summary>
    /// Broadcasts an alert to all connected SignalR clients subscribed to the tenant's alert hub.
    /// </summary>
    /// <param name="payload">The <see cref="AlertPayload"/> to push.</param>
    /// <param name="ct">Cancellation token (not forwarded to SignalR but required for interface compatibility).</param>
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

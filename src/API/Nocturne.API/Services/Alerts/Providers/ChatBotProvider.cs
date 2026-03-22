using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Providers;

/// <summary>
/// Delivers alert payloads to chat platform channels (Discord, Slack, Telegram, WhatsApp)
/// by broadcasting a SignalR event that the connected chat bot picks up and forwards
/// to the appropriate platform.
/// </summary>
internal sealed class ChatBotProvider(
    ISignalRBroadcastService broadcastService,
    ILogger<ChatBotProvider> logger)
{
    /// <summary>
    /// Channel types handled by the chat bot provider.
    /// </summary>
    public static readonly HashSet<string> SupportedChannelTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "discord_dm",
        "discord_channel",
        "slack_dm",
        "slack_channel",
        "telegram_dm",
        "telegram_group",
        "whatsapp_dm",
    };

    public async Task SendAsync(Guid deliveryId, string channelType, string destination, AlertPayload payload, CancellationToken ct)
    {
        try
        {
            await broadcastService.BroadcastAlertEventAsync("alert_dispatch", new
            {
                DeliveryId = deliveryId,
                ChannelType = channelType,
                Destination = destination,
                Payload = payload,
            });

            logger.LogDebug(
                "Chat bot alert dispatched for delivery {DeliveryId} via {ChannelType} for instance {InstanceId}",
                deliveryId, channelType, payload.InstanceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to dispatch chat bot alert for delivery {DeliveryId} via {ChannelType} for instance {InstanceId}",
                deliveryId, channelType, payload.InstanceId);
            throw;
        }
    }
}

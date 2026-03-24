using System.Net.Http.Json;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts.Providers;

internal sealed class ChatBotProvider(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<ChatBotProvider> logger)
{
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
        var webUrl = configuration["WEB_URL"];
        if (string.IsNullOrEmpty(webUrl))
        {
            logger.LogWarning("WEB_URL not configured, cannot dispatch to chat bot");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("ChatBot");
            var dispatchUrl = $"{webUrl.TrimEnd('/')}/api/v4/bot/dispatch";

            var response = await client.PostAsJsonAsync(dispatchUrl, new
            {
                DeliveryId = deliveryId,
                ChannelType = channelType,
                Destination = destination,
                Payload = payload,
            }, ct);

            response.EnsureSuccessStatusCode();

            logger.LogDebug(
                "Chat bot alert dispatched for delivery {DeliveryId} via {ChannelType}",
                deliveryId, channelType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to dispatch chat bot alert for delivery {DeliveryId} via {ChannelType}",
                deliveryId, channelType);
            throw;
        }
    }
}

import type { Chat } from "chat";
import { NocturneClient } from "../lib/nocturne-client.js";
import { AlertCard } from "../cards/alert.js";
import { createLogger } from "../lib/logger.js";
import type { AlertDispatchEvent } from "./listener.js";

const logger = createLogger();

export class AlertDeliveryHandler {
  private bot: Chat;
  private client: NocturneClient;

  constructor(bot: Chat, client: NocturneClient) {
    this.bot = bot;
    this.client = client;
  }

  async deliver(event: AlertDispatchEvent): Promise<void> {
    const { deliveryId, channelType, destination, payload } = event;

    try {
      // channelType format: "discord_dm", "slack_channel", "telegram_dm", etc.
      const isDM = channelType.endsWith("_dm");

      let target;
      if (isDM) {
        target = await this.bot.openDM(destination);
      } else {
        // Channel delivery — destination is the channel ID in "adapter:channelId" format
        target = this.bot.channel(destination);
      }

      const card = AlertCard({ payload });
      const sent = await target.post(card);

      await this.client.markDelivered(
        deliveryId,
        sent?.id,
        undefined,
      );

      logger.info(`Alert delivered via ${channelType} to ${destination}`);
    } catch (err) {
      logger.error(`Alert delivery failed for ${deliveryId}:`, err);
      await this.client.markFailed(
        deliveryId,
        err instanceof Error ? err.message : String(err),
      ).catch((e) => logger.error("Failed to report delivery failure:", e));
    }
  }
}

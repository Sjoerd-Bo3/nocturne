import type { Chat } from "chat";
import type { BotApiClient, AlertDispatchEvent } from "../types.js";
import { AlertCard } from "../cards/alert.js";
import { createLogger } from "../lib/logger.js";

const logger = createLogger();

export class AlertDeliveryHandler {
  constructor(
    private bot: Chat,
    private api: BotApiClient,
  ) {}

  async deliver(event: AlertDispatchEvent): Promise<void> {
    const { deliveryId, channelType, destination, payload } = event;

    try {
      const isDM = channelType.endsWith("_dm");
      const target = isDM
        ? await this.bot.openDM(destination)
        : this.bot.channel(destination);

      const card = AlertCard({ payload });
      const sent = await target.post(card);

      await this.api.alerts.markDelivered(deliveryId, {
        platformMessageId: sent?.id,
      });

      logger.info(`Alert delivered via ${channelType} to ${destination}`);
    } catch (err) {
      logger.error(`Alert delivery failed for ${deliveryId}:`, err);
      await this.api.alerts.markFailed(deliveryId, {
        error: err instanceof Error ? err.message : String(err),
      }).catch((e) => logger.error("Failed to report delivery failure:", e));
    }
  }
}

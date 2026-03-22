import type { Chat } from "chat";
import { NocturneClient } from "../lib/nocturne-client.js";
import { createLogger } from "../lib/logger.js";

const logger = createLogger();

export function registerAlertCommands(bot: Chat, client: NocturneClient) {
  bot.onAction("ack_alert", async (event) => {
    try {
      // TODO: resolve user's token from identity link
      const token = "";
      await client.acknowledgeAllAlerts(token, event.user?.name ?? "Unknown");
      await event.thread.post("All alerts acknowledged.");
    } catch (err) {
      logger.error("Error acknowledging alert:", err);
      await event.thread.post("Failed to acknowledge. Please try again.");
    }
  });

  bot.onAction("mute_30", async (event) => {
    try {
      // TODO: resolve user's token from identity link
      const token = "";
      await client.muteAlerts(token, event.value ?? "", 30);
      await event.thread.post("Alerts muted for 30 minutes.");
    } catch (err) {
      logger.error("Error muting alerts:", err);
      await event.thread.post("Failed to mute. Please try again.");
    }
  });

  bot.onSlashCommand("/alerts", async (event) => {
    await event.thread.post("Alert status display coming soon.");
  });
}

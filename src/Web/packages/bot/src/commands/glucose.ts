import type { Chat } from "chat";
import { NocturneClient } from "../lib/nocturne-client.js";
import { GlucoseCard } from "../cards/glucose.js";
import { createLogger } from "../lib/logger.js";

const logger = createLogger();

export function registerGlucoseCommands(bot: Chat, client: NocturneClient) {
  const handleBg = async (thread: any) => {
    try {
      // TODO: resolve identity link to get user's OAuth token
      const token = "";
      const readings = await client.getCurrentGlucose(token);

      if (!readings.length) {
        await thread.post("No recent glucose readings found.");
        return;
      }

      const card = GlucoseCard({ reading: readings[0] });
      await thread.post(card);
    } catch (err) {
      logger.error("Error handling /bg command:", err);
      await thread.post("Failed to fetch glucose data. Please try again.");
    }
  };

  bot.onSlashCommand("/bg", async (event) => {
    await handleBg(event.thread);
  });

  bot.onSlashCommand("/glucose", async (event) => {
    await handleBg(event.thread);
  });
}

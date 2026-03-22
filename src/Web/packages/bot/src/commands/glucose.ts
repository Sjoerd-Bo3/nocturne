import type { Chat } from "chat";
import type { BotApiClient } from "../types.js";
import { GlucoseCard } from "../cards/glucose.js";
import { createLogger } from "../lib/logger.js";

const logger = createLogger();

export function registerGlucoseCommands(bot: Chat, api: BotApiClient) {
  const handleBg = async (thread: any) => {
    try {
      const result = await api.sensorGlucose.getAll(1, 1);
      const readings = result.items ?? [];

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

  bot.onSlashCommand("/bg", async (event) => handleBg(event.thread));
  bot.onSlashCommand("/glucose", async (event) => handleBg(event.thread));
}

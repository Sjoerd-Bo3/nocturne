import { Chat } from "chat";
import { createDiscordAdapter } from "@chat-adapter/discord";
import { createSlackAdapter } from "@chat-adapter/slack";
import { createTelegramAdapter } from "@chat-adapter/telegram";
import { createWhatsAppAdapter } from "@chat-adapter/whatsapp";
import { createPgState } from "@chat-adapter/state-postgres";
import { loadConfig, type BotConfig } from "./lib/config.js";
import { createLogger } from "./lib/logger.js";
import { NocturneClient } from "./lib/nocturne-client.js";
import { registerAllCommands } from "./commands/index.js";

const logger = createLogger();

export function createBot() {
  const config = loadConfig();

  const adapters: Record<string, any> = {};

  if (config.platforms.discord) {
    logger.info("Enabling Discord adapter");
    adapters.discord = createDiscordAdapter();
  }
  if (config.platforms.slack) {
    logger.info("Enabling Slack adapter");
    adapters.slack = createSlackAdapter();
  }
  if (config.platforms.telegram) {
    logger.info("Enabling Telegram adapter");
    adapters.telegram = createTelegramAdapter();
  }
  if (config.platforms.whatsapp) {
    logger.info("Enabling WhatsApp adapter");
    adapters.whatsapp = createWhatsAppAdapter();
  }

  if (Object.keys(adapters).length === 0) {
    logger.warn("No platform adapters configured. Bot will start without any platforms.");
  }

  const bot = new Chat({
    userName: "nocturne",
    adapters,
    state: config.postgres.connectionString
      ? createPgState({ connectionString: config.postgres.connectionString })
      : undefined,
  });

  const client = new NocturneClient(config.apiUrl, config.serviceToken);

  registerAllCommands(bot, client);

  // @mention fallback — respond with current BG
  bot.onNewMention(async (thread, message) => {
    try {
      // TODO: resolve user's token from identity link
      const token = "";
      const readings = await client.getCurrentGlucose(token);

      if (!readings.length) {
        await thread.post("No recent glucose readings found.");
        return;
      }

      const { GlucoseCard } = await import("./cards/glucose.js");
      const card = GlucoseCard({ reading: readings[0] });
      await thread.post(card);
    } catch (err) {
      logger.error("Error handling mention:", err);
      await thread.post("Failed to fetch glucose data.");
    }
  });

  return { bot, client, config };
}

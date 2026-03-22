import { Chat } from "chat";
import { createDiscordAdapter } from "@chat-adapter/discord";
import { createSlackAdapter } from "@chat-adapter/slack";
import { createTelegramAdapter } from "@chat-adapter/telegram";
import { createWhatsAppAdapter } from "@chat-adapter/whatsapp";
import { createPgState } from "@chat-adapter/state-postgres";
import { loadConfig, type BotConfig } from "./lib/config.js";
import { createLogger } from "./lib/logger.js";
import { NocturneClient } from "./lib/nocturne-client.js";

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

  const client = new NocturneClient(config.apiUrl);

  return { bot, client, config };
}

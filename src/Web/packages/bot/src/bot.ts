import { Chat } from "chat";
import { createDiscordAdapter } from "@chat-adapter/discord";
import { createSlackAdapter } from "@chat-adapter/slack";
import { createTelegramAdapter } from "@chat-adapter/telegram";
import { createWhatsAppAdapter } from "@chat-adapter/whatsapp";
import { createPostgresState } from "@chat-adapter/state-pg";
import { createLogger } from "./lib/logger.js";

const logger = createLogger();

export interface BotOptions {
  platforms?: {
    discord?: boolean;
    slack?: boolean;
    telegram?: boolean;
    whatsapp?: boolean;
  };
  postgresUrl: string;
}

export function createBot(options: BotOptions): Chat {
  const adapters: Record<string, any> = {};
  const platforms = options.platforms ?? {};

  if (platforms.discord) {
    logger.info("Enabling Discord adapter");
    adapters.discord = createDiscordAdapter();
  }
  if (platforms.slack) {
    logger.info("Enabling Slack adapter");
    adapters.slack = createSlackAdapter();
  }
  if (platforms.telegram) {
    logger.info("Enabling Telegram adapter");
    adapters.telegram = createTelegramAdapter();
  }
  if (platforms.whatsapp) {
    logger.info("Enabling WhatsApp adapter");
    adapters.whatsapp = createWhatsAppAdapter();
  }

  if (Object.keys(adapters).length === 0) {
    logger.warn("No platform adapters configured.");
  }

  return new Chat({
    userName: "nocturne",
    adapters,
    state: createPostgresState({ url: options.postgresUrl }),
  });
}

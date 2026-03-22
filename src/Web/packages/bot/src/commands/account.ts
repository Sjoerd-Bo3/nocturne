import type { Chat } from "chat";
import { createLogger } from "../lib/logger.js";

const logger = createLogger();

export function registerAccountCommands(bot: Chat) {
  bot.onSlashCommand("/connect", async (event) => {
    await event.thread.post(
      "Account linking is not yet available. This feature is coming soon.",
    );
  });

  bot.onSlashCommand("/disconnect", async (event) => {
    await event.thread.post(
      "Account unlinking is not yet available. This feature is coming soon.",
    );
  });

  bot.onSlashCommand("/status", async (event) => {
    await event.thread.post(
      "Status checking is not yet available. This feature is coming soon.",
    );
  });
}

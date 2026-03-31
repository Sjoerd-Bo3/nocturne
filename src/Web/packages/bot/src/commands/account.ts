import type { Chat } from "chat";
import { createStateToken } from "../lib/state-tokens.js";

export function registerAccountCommands(bot: Chat, nocturneUrl: string) {
  bot.onSlashCommand("/connect", async (event) => {
    const platformIdentity = `${event.adapter.name}:${event.user.userId}`;
    const token = createStateToken(platformIdentity);
    const link = `${nocturneUrl}/auth/bot/authorize?state=${token}`;

    const message = `To connect your Nocturne account, click here: ${link}\n\nThis link expires in 10 minutes.`;

    try {
      await event.channel.postEphemeral(event.user, message, { fallbackToDM: true });
    } catch {
      await event.channel.post(message);
    }
  });

  bot.onSlashCommand("/disconnect", async (event) => {
    await event.channel.post("Account unlinking is not yet available.");
  });

  bot.onSlashCommand("/status", async (event) => {
    await event.channel.post("Status checking is not yet available.");
  });
}

import { createBot, AlertDeliveryHandler } from "@nocturne/bot";
import type { BotApiClient, AlertDispatchEvent } from "@nocturne/bot";
import { env } from "$env/dynamic/private";
import type { Chat } from "chat";

let botInstance: Chat | null = null;

export function getBot(): Chat {
	if (!botInstance) {
		botInstance = createBot({
			platforms: {
				discord: !!env.DISCORD_TOKEN,
				slack: !!env.SLACK_BOT_TOKEN && !!env.SLACK_SIGNING_SECRET,
				telegram: !!env.TELEGRAM_BOT_TOKEN,
				whatsapp: !!env.WHATSAPP_ACCESS_TOKEN,
			},
			postgresConnectionString: env.ConnectionStrings__nocturne_postgres,
		});
	}
	return botInstance;
}

export async function handleBotDispatch(event: AlertDispatchEvent, api: BotApiClient): Promise<void> {
	const bot = getBot();
	const handler = new AlertDeliveryHandler(bot, api);
	await handler.deliver(event);
}

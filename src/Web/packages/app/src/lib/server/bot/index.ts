import { createBot, AlertDeliveryHandler, type BotOptions } from "@nocturne/bot";
import type { BotApiClient, AlertDispatchEvent } from "@nocturne/bot";
import { env } from "$env/dynamic/private";

type Bot = ReturnType<typeof createBot>;

let botInstance: Bot | null = null;

export function getBot(): Bot {
	if (!botInstance) {
		const options: BotOptions = {
			platforms: {
				discord: !!env.DISCORD_BOT_TOKEN,
				slack: !!env.SLACK_BOT_TOKEN && !!env.SLACK_SIGNING_SECRET,
				telegram: !!env.TELEGRAM_BOT_TOKEN,
				whatsapp: !!env.WHATSAPP_ACCESS_TOKEN,
			},
			postgresUrl: env.ConnectionStrings__nocturne_postgres ?? "",
		};
		botInstance = createBot(options);
	}
	return botInstance;
}

export async function handleBotDispatch(event: AlertDispatchEvent, api: BotApiClient): Promise<void> {
	const bot = getBot();
	const handler = new AlertDeliveryHandler(bot, api);
	await handler.deliver(event);
}

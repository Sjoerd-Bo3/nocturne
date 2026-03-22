import type { RequestHandler } from "./$types";
import { env } from "$env/dynamic/private";

const BOT_URL = env.BOT_WEBHOOK_URL ?? "http://localhost:3001";

export const POST: RequestHandler = async ({ request }) => {
	const res = await fetch(`${BOT_URL}/webhooks/telegram`, {
		method: "POST",
		headers: request.headers,
		body: await request.arrayBuffer(),
	});
	return new Response(res.body, {
		status: res.status,
		headers: res.headers,
	});
};

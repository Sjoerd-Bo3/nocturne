import type { RequestHandler } from "./$types";
import { env } from "$env/dynamic/private";

const BOT_URL = env.BOT_WEBHOOK_URL ?? "http://localhost:3001";

export const POST: RequestHandler = async ({ request }) => {
	try {
		const res = await fetch(`${BOT_URL}/webhooks/whatsapp`, {
			method: "POST",
			headers: request.headers,
			body: await request.arrayBuffer(),
		});
		return new Response(res.body, {
			status: res.status,
			headers: res.headers,
		});
	} catch {
		return new Response(JSON.stringify({ error: "Bot service unavailable" }), {
			status: 502,
			headers: { "Content-Type": "application/json" },
		});
	}
};

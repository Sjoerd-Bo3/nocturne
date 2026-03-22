import type { RequestHandler } from "./$types";
import { handleBotDispatch } from "$lib/server/bot";
import type { AlertDispatchEvent } from "@nocturne/bot";

export const POST: RequestHandler = async ({ request, locals }) => {
	try {
		const event: AlertDispatchEvent = await request.json();
		await handleBotDispatch(event, locals.apiClient);
		return new Response(null, { status: 204 });
	} catch (err) {
		console.error("Bot dispatch failed:", err);
		return new Response(JSON.stringify({ error: "Dispatch failed" }), {
			status: 500,
			headers: { "Content-Type": "application/json" },
		});
	}
};

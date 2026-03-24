import type { RequestHandler } from "./$types";
import { handleBotDispatch } from "$lib/server/bot";
import type { AlertDispatchEvent, BotApiClient } from "@nocturne/bot";

export const POST: RequestHandler = async ({ request, locals }) => {
	try {
		const event: AlertDispatchEvent = await request.json();
		const api = locals.apiClient;
		const botApiClient: BotApiClient = {
			sensorGlucose: {
				async getAll(page?: number, pageSize?: number) {
					const result = await api.sensorGlucose.getAll(
						undefined, undefined,
						pageSize, page !== undefined && pageSize !== undefined ? page * pageSize : undefined
					);
					return { items: result.data as { sgv?: number; direction?: string; mills?: number; dateString?: string; delta?: number }[] | undefined };
				},
			},
			alerts: {
				acknowledgeAlerts: (body) => api.alerts.acknowledge({ acknowledgedBy: body.acknowledgedBy }),
			},
			chatIdentity: {
				resolve: (platform, platformUserId) => api.chatIdentity.resolve(platform, platformUserId),
			},
		};
		await handleBotDispatch(event, botApiClient);
		return new Response(null, { status: 204 });
	} catch (err) {
		console.error("Bot dispatch failed:", err);
		return new Response(JSON.stringify({ error: "Dispatch failed" }), {
			status: 500,
			headers: { "Content-Type": "application/json" },
		});
	}
};

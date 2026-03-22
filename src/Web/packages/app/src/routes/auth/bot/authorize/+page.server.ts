import type { PageServerLoad, Actions } from "./$types";
import { redirect, fail } from "@sveltejs/kit";
import { resolveStateToken } from "@nocturne/bot";

export const load: PageServerLoad = async ({ url, locals }) => {
	const state = url.searchParams.get("state");
	if (!state) {
		throw redirect(303, "/");
	}

	if (!locals.isAuthenticated) {
		const returnUrl = `/auth/bot/authorize?state=${state}`;
		throw redirect(303, `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`);
	}

	return { state };
};

export const actions: Actions = {
	default: async ({ request, locals }) => {
		if (!locals.isAuthenticated || !locals.user) {
			return fail(401, { error: "Not authenticated" });
		}

		const data = await request.formData();
		const state = data.get("state") as string;
		if (!state) {
			return fail(400, { error: "Missing state parameter." });
		}

		const platformIdentity = resolveStateToken(state);
		if (!platformIdentity) {
			return fail(400, { error: "Link expired or invalid. Please run /connect again in your chat app." });
		}

		// platformIdentity format: "discord:abc123"
		const colonIndex = platformIdentity.indexOf(":");
		if (colonIndex === -1) {
			return fail(400, { error: "Invalid platform identity format." });
		}

		const platform = platformIdentity.substring(0, colonIndex);
		const platformUserId = platformIdentity.substring(colonIndex + 1);

		try {
			await locals.apiClient.chatIdentity.createLink({
				platform,
				platformUserId,
				nocturneUserId: locals.user.subjectId,
			});
		} catch (err) {
			console.error("Failed to create identity link:", err);
			return fail(500, { error: "Failed to link account. Please try again." });
		}

		return { success: true };
	},
};

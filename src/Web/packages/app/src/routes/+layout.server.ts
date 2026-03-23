import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";
import { checkOnboarding } from "$lib/server/onboarding-check";

/** Route prefixes that bypass the onboarding gate */
const BYPASS_PREFIXES = ["/auth", "/api", "/settings/setup", "/clock"];

/**
 * Root layout server load function
 * Provides user data to all routes and enforces the onboarding gate
 */
export const load: LayoutServerLoad = async ({ locals, cookies, url }) => {
  // Onboarding gate: only check for authenticated users on non-bypassed routes
  if (locals.isAuthenticated && locals.apiClient) {
    const pathname = url.pathname;
    const isBypassed = BYPASS_PREFIXES.some((prefix) =>
      pathname.startsWith(prefix)
    );

    if (!isBypassed) {
      const onboarding = await checkOnboarding(locals.apiClient, cookies);
      if (!onboarding.isComplete) {
        throw redirect(303, "/settings/setup");
      }
    }
  }

  return {
    user: locals.user,
    isAuthenticated: locals.isAuthenticated,
  };
};

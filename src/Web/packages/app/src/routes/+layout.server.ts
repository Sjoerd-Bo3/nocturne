import { redirect } from "@sveltejs/kit";
import type { LayoutServerLoad } from "./$types";
import { checkOnboarding } from "$lib/server/onboarding-check";

/** Route prefixes that bypass the onboarding gate */
const BYPASS_PREFIXES = ["/auth", "/api", "/settings/setup", "/clock"];

/**
 * Root layout server load function
 * Provides user data to all routes and enforces the onboarding gate.
 * Also detects setup mode (fresh database) and redirects to passkey setup.
 */
export const load: LayoutServerLoad = async ({ locals, cookies, url }) => {
  const pathname = url.pathname;

  // Setup mode gate: if user is NOT authenticated and NOT already on a setup/auth page,
  // check if the instance requires initial setup
  if (!locals.isAuthenticated && locals.apiClient) {
    const isBypassed = BYPASS_PREFIXES.some((prefix) =>
      pathname.startsWith(prefix)
    );

    if (!isBypassed) {
      try {
        const status = await locals.apiClient.passkey.getAuthStatus();
        if (status?.setupRequired) {
          throw redirect(303, "/settings/setup/passkey");
        }
        if (status?.recoveryMode) {
          throw redirect(303, "/auth/recovery");
        }
      } catch (err) {
        // Re-throw redirects
        if (err && typeof err === "object" && "status" in err && (err as any).status === 303) {
          throw err;
        }
        // Fail open — don't trap users if the API is down
      }
    }
  }

  // Onboarding gate: only check for authenticated users on non-bypassed routes
  if (locals.isAuthenticated && locals.apiClient) {
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

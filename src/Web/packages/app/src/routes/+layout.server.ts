import type { LayoutServerLoad } from "./$types";

/**
 * Root layout server load function
 * Provides user data to all routes
 */
export const load: LayoutServerLoad = async ({ locals }) => {
  let tenantCount = 0;
  if (locals.isAuthenticated) {
    try {
      const tenants = await locals.apiClient.myTenants.getMyTenants();
      tenantCount = tenants?.length ?? 0;
    } catch {
      // Non-critical — sidebar just won't show tenants link
    }
  }

  return {
    user: locals.user,
    isAuthenticated: locals.isAuthenticated,
    tenantCount,
  };
};

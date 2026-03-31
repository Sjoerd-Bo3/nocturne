/**
 * Route prefixes that bypass authentication, site security, setup mode,
 * and onboarding gates. Define once, use everywhere.
 */
export const PUBLIC_ROUTE_PREFIXES = [
  "/auth",
  "/api",
  "/settings/setup",
  "/clock",
  "/invite",
] as const;

/**
 * Static asset paths that bypass all middleware. These are not pages —
 * they're framework/browser resources that should never be gated.
 */
export const STATIC_ASSET_PREFIXES = [
  "/_app",
  "/assets",
  "/favicon.ico",
] as const;

/** Check if a pathname is a public route or static asset. */
export function isPublicRoute(pathname: string): boolean {
  return (
    pathname === "/" ||
    PUBLIC_ROUTE_PREFIXES.some((p) => pathname.startsWith(p)) ||
    STATIC_ASSET_PREFIXES.some((p) => pathname.startsWith(p))
  );
}

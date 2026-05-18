/**
 * Normalizes an instance URL by removing trailing slashes.
 * Returns empty string if input is empty (for SSR safety).
 */
function normalize(instanceUrl: string): string {
  return instanceUrl.replace(/\/+$/, "");
}

/**
 * Builds the xDrip+ deep link that triggers auto-configuration of the
 * Nocturne uploader.
 *
 * @param instanceUrl — the Nocturne instance origin URL (with or without trailing slash)
 */
export function buildXdripDeepLink(instanceUrl: string): string {
  const normalized = normalize(instanceUrl);
  return `xdrip://connect/nocturne?url=${encodeURIComponent(normalized)}`;
}

/**
 * Builds the public connect page URL that a phone QR scanner can open.
 * This URL serves a trampoline page that redirects to the xDrip+ deep link.
 */
export function buildConnectPageUrl(instanceUrl: string): string {
  return `${normalize(instanceUrl)}/connect/xdrip`;
}

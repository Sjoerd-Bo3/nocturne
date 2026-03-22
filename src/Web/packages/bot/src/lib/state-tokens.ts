const tokens = new Map<string, { platformIdentity: string; createdAt: number }>();
const TTL_MS = 10 * 60 * 1000; // 10 minutes

export function createStateToken(platformIdentity: string): string {
  const token = crypto.randomUUID();
  tokens.set(token, { platformIdentity, createdAt: Date.now() });
  return token;
}

export function resolveStateToken(token: string): string | null {
  const entry = tokens.get(token);
  if (!entry) return null;
  if (Date.now() - entry.createdAt > TTL_MS) {
    tokens.delete(token);
    return null;
  }
  tokens.delete(token); // Single use
  return entry.platformIdentity;
}

// Periodic cleanup of expired tokens
setInterval(() => {
  const now = Date.now();
  for (const [key, value] of tokens) {
    if (now - value.createdAt > TTL_MS) tokens.delete(key);
  }
}, 60_000);

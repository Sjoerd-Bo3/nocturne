import type { SlashCommandEvent } from "chat";
import { getUnscopedApi, runWithResolvedLink, type ResolvedLink } from "./request-context.js";
import type { DirectoryCandidate } from "../types.js";

/**
 * Resolves the current Discord (or other chat platform) user to a specific
 * Nocturne tenant link, then invokes `callback` with the resolved link and a
 * nested request context that makes `getApi()` return the tenant-scoped api
 * client.
 *
 * This is the chokepoint that every tenant-scoped slash command should go
 * through. Handlers that don't wrap their body in requireLink will have
 * `getApi()` throw.
 *
 * ## Resolution logic
 *
 * Parses `event.text` (trimmed, lowercased) as an optional label argument
 * and calls the directory.resolve endpoint via the unscoped api client:
 *
 * - **Zero candidates** → posts ephemeral "your account isn't linked yet, run
 *   /connect" and returns null without invoking callback.
 *
 * - **One candidate** → ignores any label arg (even if it doesn't match —
 *   assume the user typed a different platform's label by mistake; give them
 *   the only thing they have linked) and invokes callback with that link.
 *
 * - **Multiple candidates + label arg matches exactly one** → invokes
 *   callback with the matched link.
 *
 * - **Multiple candidates + no label arg + a default exists** → invokes
 *   callback with the default.
 *
 * - **Multiple candidates + no label arg + no default** → posts an ephemeral
 *   disambiguation message listing each label and display name, returns null.
 *
 * - **Multiple candidates + label arg does not match any** → posts an
 *   ephemeral "no link named X, try one of: ..." and returns null.
 *
 * Returns `null` on any non-success branch so callers can detect whether
 * their work actually ran.
 *
 * @example
 * // Basic tenant-scoped handler
 * bot.onSlashCommand("/bg", async (event) => {
 *   await requireLink(event, async (link) => {
 *     const api = getApi(); // scoped to link.tenantSlug
 *     const result = await api.sensorGlucose.getAll(undefined, undefined, 1);
 *     await event.channel.post(`Latest BG for ${link.displayName}: ${result.data?.[0]?.mgdl}`);
 *   });
 * });
 *
 * @example
 * // Detecting whether the callback actually ran
 * bot.onSlashCommand("/refresh", async (event) => {
 *   const result = await requireLink(event, async (link) => {
 *     return await doWork(link);
 *   });
 *   if (result === null) {
 *     // requireLink already posted an ephemeral explaining what went wrong.
 *     return;
 *   }
 *   // use result...
 * });
 *
 * @example
 * // Zero candidates branch:
 * //   User invokes /bg, never ran /connect.
 * //   Ephemeral: "Your Discord account isn't linked to a Nocturne account
 * //   yet. Run `/connect` to get started."
 *
 * @example
 * // Single candidate branch:
 * //   User has one link labelled "home". They run `/bg wrong-label`.
 * //   requireLink ignores the bad label and resolves to "home" anyway.
 *
 * @example
 * // Multi + label match:
 * //   User has "home" and "work". They run `/bg work`.
 * //   requireLink resolves to the "work" link.
 *
 * @example
 * // Multi + default:
 * //   User has "home" (default) and "work". They run `/bg`.
 * //   requireLink resolves to "home".
 *
 * @example
 * // Multi + ambiguous:
 * //   User has "home" and "work", neither default. They run `/bg`.
 * //   Ephemeral: "You have multiple linked Nocturne accounts: `home` (Home),
 * //   `work` (Work). Use `/bg <label>` to pick one, or set a default in
 * //   Settings → Integrations → Discord."
 *
 * @example
 * // Multi + label not found:
 * //   User has "home" and "work". They run `/bg beach`.
 * //   Ephemeral: "No linked account named `beach`. Your linked accounts:
 * //   `home`, `work`."
 */
export async function requireLink<T>(
  event: SlashCommandEvent,
  callback: (link: ResolvedLink) => Promise<T>,
): Promise<T | null> {
  const platform = event.adapter.name;
  const platformUserId = event.user.userId;
  const labelArg = event.text?.trim().toLowerCase() || null;

  const candidates = await getUnscopedApi().directory.resolve(platform, platformUserId);

  if (!candidates || candidates.length === 0) {
    await event.channel.postEphemeral(
      event.user,
      "Your Discord account isn't linked to a Nocturne account yet. Run `/connect` to get started.",
      { fallbackToDM: true },
    );
    return null;
  }

  const picked = pickCandidate(candidates, labelArg);

  if (picked === "ambiguous") {
    const labelList = candidates
      .map((c) => `\`${c.label}\` (${c.displayName})`)
      .join(", ");
    await event.channel.postEphemeral(
      event.user,
      `You have multiple linked Nocturne accounts: ${labelList}. Use \`${event.command} <label>\` to pick one, or set a default in Settings → Integrations → Discord.`,
      { fallbackToDM: true },
    );
    return null;
  }

  if (picked === "not-found") {
    const availableLabels = candidates.map((c) => `\`${c.label}\``).join(", ");
    await event.channel.postEphemeral(
      event.user,
      `No linked account named \`${labelArg}\`. Your linked accounts: ${availableLabels}.`,
      { fallbackToDM: true },
    );
    return null;
  }

  const link: ResolvedLink = {
    id: picked.id,
    tenantId: picked.tenantId,
    tenantSlug: picked.tenantSlug,
    nocturneUserId: picked.nocturneUserId,
    label: picked.label,
    displayName: picked.displayName,
  };

  return await runWithResolvedLink(link, () => callback(link));
}

/**
 * Selects a single DirectoryCandidate from a non-empty list, based on an
 * optional label argument. Returns "ambiguous" if no disambiguation is
 * possible, or "not-found" if the label arg doesn't match any candidate.
 *
 * Pure function — no side effects, easy to reason about.
 */
function pickCandidate(
  candidates: DirectoryCandidate[],
  labelArg: string | null,
): DirectoryCandidate | "ambiguous" | "not-found" {
  // Single candidate: always return it, ignoring any label arg the user typed.
  if (candidates.length === 1) return candidates[0]!;

  // Label provided: exact match or nothing.
  if (labelArg) {
    const match = candidates.find((c) => c.label === labelArg);
    return match ?? "not-found";
  }

  // No label and multiple candidates: ambiguous.
  return "ambiguous";
}

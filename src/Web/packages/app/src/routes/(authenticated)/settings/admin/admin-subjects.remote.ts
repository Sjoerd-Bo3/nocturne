/**
 * Admin Subject Remote Functions
 *
 * Server-side wrappers around the generated SubjectAdminClient for use
 * from the platform admin Users tab.
 */

import { z } from "zod";
import { command, getRequestEvent } from "$app/server";

function getApiClient() {
  const event = getRequestEvent();
  if (!event?.locals?.apiClient) {
    throw new Error("API client not configured");
  }
  return event.locals.apiClient;
}

export const setPlatformAdmin = command(
  z.object({
    subjectId: z.string().uuid(),
    isPlatformAdmin: z.boolean(),
  }),
  async ({ subjectId, isPlatformAdmin }) => {
    return getApiClient().subjectAdmin.setPlatformAdmin(subjectId, {
      isPlatformAdmin,
    });
  }
);

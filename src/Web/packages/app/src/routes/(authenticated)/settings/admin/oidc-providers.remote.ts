/**
 * OIDC Provider Admin Remote Functions
 *
 * Server-side wrappers around the generated OidcProviderAdminClient for use
 * from the platform admin Identity Providers tab.
 */

import { z } from "zod";
import { query, command, getRequestEvent } from "$app/server";

function getApiClient() {
  const event = getRequestEvent();
  if (!event?.locals?.apiClient) {
    throw new Error("API client not configured");
  }
  return event.locals.apiClient;
}

export const getOidcProviders = query(async () => {
  return getApiClient().oidcProviderAdmin.getAll();
});

export const getConfigManaged = query(async () => {
  const result = await getApiClient().oidcProviderAdmin.getConfigManaged();
  return result.isConfigManaged ?? false;
});

const createSchema = z.object({
  name: z.string().min(1),
  issuerUrl: z.string().url(),
  clientId: z.string().min(1),
  clientSecret: z.string().optional(),
  scopes: z.array(z.string()).optional(),
  defaultRoles: z.array(z.string()).optional(),
  isEnabled: z.boolean().default(true),
  displayOrder: z.number().default(0),
  icon: z.string().optional(),
  buttonColor: z.string().optional(),
});

export const createOidcProvider = command(createSchema, async (data) => {
  return getApiClient().oidcProviderAdmin.create(data);
});

const updateSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  issuerUrl: z.string().url(),
  clientId: z.string().min(1),
  clientSecret: z.string().optional(),
  scopes: z.array(z.string()).optional(),
  defaultRoles: z.array(z.string()).optional(),
  isEnabled: z.boolean(),
  displayOrder: z.number(),
  icon: z.string().optional(),
  buttonColor: z.string().optional(),
});

export const updateOidcProvider = command(updateSchema, async ({ id, ...data }) => {
  return getApiClient().oidcProviderAdmin.update(id, data);
});

export const deleteOidcProvider = command(z.string().uuid(), async (id) => {
  return getApiClient().oidcProviderAdmin.delete(id);
});

export const enableOidcProvider = command(z.string().uuid(), async (id) => {
  return getApiClient().oidcProviderAdmin.enable(id);
});

export const disableOidcProvider = command(z.string().uuid(), async (id) => {
  return getApiClient().oidcProviderAdmin.disable(id);
});

export const testOidcProviderConfig = command(
  z.object({
    issuerUrl: z.string().url(),
    clientId: z.string().min(1),
    clientSecret: z.string().optional(),
  }),
  async (data) => {
    return getApiClient().oidcProviderAdmin.testUnsaved(data);
  }
);

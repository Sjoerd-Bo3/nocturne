import { getRequestEvent, query, command } from '$app/server';
import { error, redirect } from '@sveltejs/kit';
import { z } from 'zod';
import { type CreateRoleRequest, type UpdateRoleRequest, type SetMemberRolesRequest, type SetMemberPermissionsRequest } from '$api';

/** List roles for the current tenant. */
export const getRoles = query(async () => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    return await apiClient.roles.getRoles();
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in roles.getRoles:', err);
    throw error(500, 'Failed to get roles');
  }
});

/** Create a custom role. */
export const createRole = command(z.object({
  name: z.string(),
  description: z.string().optional(),
  permissions: z.array(z.string()),
}), async (request) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    const result = await apiClient.roles.createRole(request as CreateRoleRequest);
    await getRoles(undefined).refresh();
    return result;
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in roles.createRole:', err);
    throw error(500, 'Failed to create role');
  }
});

/** Update a role. */
export const updateRole = command(z.object({
  id: z.string(),
  name: z.string(),
  description: z.string().optional(),
  permissions: z.array(z.string()),
}), async ({ id, ...request }) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    const result = await apiClient.roles.updateRole(id, request as UpdateRoleRequest);
    await getRoles(undefined).refresh();
    return result;
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in roles.updateRole:', err);
    throw error(500, 'Failed to update role');
  }
});

/** Delete a role. */
export const deleteRole = command(z.string(), async (id) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    await apiClient.roles.deleteRole(id);
    await getRoles(undefined).refresh();
    return { success: true };
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in roles.deleteRole:', err);
    throw error(500, 'Failed to delete role');
  }
});

/** Set roles for a member (replaces all role assignments). */
export const setMemberRoles = command(z.object({
  memberId: z.string(),
  roleIds: z.array(z.string()),
}), async ({ memberId, roleIds }) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    await apiClient.memberInvites.setMemberRoles(memberId, { roleIds } as SetMemberRolesRequest);
    return { success: true };
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in memberInvites.setMemberRoles:', err);
    throw error(500, 'Failed to set member roles');
  }
});

/** Set direct permissions for a member. */
export const setMemberPermissions = command(z.object({
  memberId: z.string(),
  directPermissions: z.array(z.string()),
}), async ({ memberId, directPermissions }) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    await apiClient.memberInvites.setMemberPermissions(memberId, { directPermissions } as SetMemberPermissionsRequest);
    return { success: true };
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in memberInvites.setMemberPermissions:', err);
    throw error(500, 'Failed to set member permissions');
  }
});

/** Get effective permissions for a member. */
export const getEffectivePermissions = query(z.string(), async (memberId) => {
  const apiClient = getRequestEvent().locals.apiClient;
  try {
    return await apiClient.memberInvites.getEffectivePermissions(memberId);
  } catch (err) {
    const status = (err as any)?.status;
    if (status === 401) { const { url } = getRequestEvent(); throw redirect(302, `/auth/login?returnUrl=${encodeURIComponent(url.pathname + url.search)}`); }
    if (status === 403) throw error(403, 'Forbidden');
    console.error('Error in memberInvites.getEffectivePermissions:', err);
    throw error(500, 'Failed to get effective permissions');
  }
});

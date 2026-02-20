/**
 * Hand-written remote functions for services that don't have generated equivalents.
 * For generated remote functions, use $api/generated/services.generated.remote or
 * $api/generated/connectorStatus.generated.remote instead.
 */
import { getRequestEvent, query } from '$app/server';
import { error } from '@sveltejs/kit';

/** Gets the current status and metrics for all registered connectors */
export const getConnectorStatuses = query(async () => {
	const { locals } = getRequestEvent();
	const { apiClient } = locals;
	try {
		return await apiClient.connectorStatus.getStatus();
	} catch (err) {
		console.error('Error in connectorStatus.getStatus:', err);
		throw error(500, 'Failed to get status');
	}
});

/**
 * Remote functions for manual compatibility testing
 */
import { getRequestEvent, query } from '$app/server';
import { error } from '@sveltejs/kit';
import type { ManualTestRequest } from '$lib/api';
import { ManualTestRequestSchema } from '$lib/api/generated/schemas';

/**
 * Run a manual compatibility test between Nightscout and Nocturne
 */
export const runCompatibilityTest = query(ManualTestRequestSchema, async (request) => {
	const { locals } = getRequestEvent();
	const { apiClient } = locals;
	try {
		// Call the test endpoint via the API client
		const result = await apiClient.compatibility.testApiComparison(request as ManualTestRequest);
		return result;
	} catch (err) {
		console.error('Error running compatibility test:', err);
		if ((err as any).status) {
			throw err;
		}
		throw error(500, 'Failed to run compatibility test');
	}
});

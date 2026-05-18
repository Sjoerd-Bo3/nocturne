/**
 * Remote functions for the Nightscout transition dashboard.
 *
 * TODO: Replace with NSwag-generated client call once Aspire regenerates the
 * API client (the NightscoutTransitionController was added after the last
 * NSwag generation). At that point this should become:
 *   return await apiClient.nightscoutTransition.getTransitionStatus();
 */
import { getRequestEvent, query } from '$app/server';
import { error } from '@sveltejs/kit';

export interface MigrationStatusInfo {
	recordCounts: Record<string, number>;
	lastSyncTime: string | null;
	isComplete: boolean;
}

export interface WriteBackHealthInfo {
	requestsLast24h: number;
	successesLast24h: number;
	failuresLast24h: number;
	circuitBreakerOpen: boolean;
	lastSuccessTime: string | null;
}

export interface DisconnectRecommendation {
	status: 'not-ready' | 'almost-ready' | 'safe';
	blockers: string[];
	stabilityDaysRemaining: number | null;
}

export interface CompatibilityInfo {
	proxyEnabled: boolean;
	compatibilityScore: number | null;
	totalComparisons: number;
	discrepancies: number;
}

export interface NightscoutTransitionStatus {
	migration: MigrationStatusInfo;
	writeBack: WriteBackHealthInfo;
	compatibility: CompatibilityInfo | null;
	recommendation: DisconnectRecommendation;
}

/**
 * Get the current Nightscout transition status including migration progress,
 * write-back health, and disconnect readiness recommendation.
 */
export const getTransitionStatus = query(async () => {
	const event = getRequestEvent();
	const { apiClient } = event.locals;

	try {
		// Raw fetch until NSwag client is regenerated with this endpoint
		const response = await event.fetch(
			`${apiClient.baseUrl}/api/v4/nightscout-transition/status`
		);

		if (!response.ok) {
			throw { status: response.status };
		}

		return (await response.json()) as NightscoutTransitionStatus;
	} catch (err: unknown) {
		console.error('Error loading transition status:', err);
		if (err && typeof err === 'object' && 'status' in err) {
			throw err;
		}
		throw error(500, 'Failed to load Nightscout transition status');
	}
});

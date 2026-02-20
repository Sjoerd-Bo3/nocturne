/**
 * Remote functions for day-in-review report
 * Fetches sensor glucose, boluses, and carb intakes for a specific day
 */
import { z } from 'zod';
import { getRequestEvent, query } from '$app/server';
import { error } from '@sveltejs/kit';
import { getApsSnapshots } from '$api/generated/deviceStatus.generated.remote';

/**
 * Get day-in-review data for a specific date
 */
export const getDayInReviewData = query(
	z.string(), // date string in ISO format (YYYY-MM-DD)
	async (dateParam) => {
		if (!dateParam) {
			throw error(400, 'Date parameter is required');
		}

		const date = new Date(dateParam);
		if (isNaN(date.getTime())) {
			throw error(400, 'Invalid date parameter');
		}

		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		// Calculate day boundaries in local time
		// Using the date string directly to avoid timezone confusion -
		// if someone in Australia wants to see Dec 10th, they should see Dec 10th data
		const dayStart = new Date(date);
		dayStart.setHours(0, 0, 0, 0);

		const dayEnd = new Date(date);
		dayEnd.setHours(23, 59, 59, 999);

		// Fetch v4 data + APS snapshots for historical predictions
		const [entriesResponse, bolusResponse, carbResponse, apsResponse] = await Promise.all([
			apiClient.glucose.getSensorGlucose(dayStart.getTime(), dayEnd.getTime(), 10000),
			apiClient.insulin.getBoluses(dayStart.getTime(), dayEnd.getTime(), 1000),
			apiClient.nutrition.getCarbIntakes(dayStart.getTime(), dayEnd.getTime(), 1000),
			getApsSnapshots({ from: dayStart.getTime(), to: dayEnd.getTime(), limit: 1000, sort: 'mills_asc' }),
		]);

		const entries = entriesResponse.data ?? [];
		const boluses = bolusResponse.data ?? [];
		const carbIntakes = carbResponse.data ?? [];
		const apsSnapshots = apsResponse.data ?? [];

		// Calculate analysis from the backend - this includes treatmentSummary
		const analysis = entries.length > 0
			? await apiClient.statistics.analyzeGlucoseDataExtended({
					entries,
					boluses,
					carbIntakes,
					population: 0 as const, // Type1Adult
				})
			: null;

		// Use the treatmentSummary from analysis (if available) to avoid redundant API call
		// The backend AnalyzeGlucoseDataExtended already calculates TreatmentSummary
		// If no entries but we have boluses/carbIntakes, calculate treatmentSummary directly
		const treatmentSummary = analysis?.treatmentSummary
			?? ((boluses.length > 0 || carbIntakes.length > 0)
				? await apiClient.statistics.calculateTreatmentSummary({ boluses, carbIntakes })
				: null);

		return {
			date: dateParam,
			entries,
			boluses,
			carbIntakes,
			analysis,
			treatmentSummary,
			apsSnapshots,
			dateRange: {
				from: dayStart.toISOString(),
				to: dayEnd.toISOString(),
			},
		};
	}
);


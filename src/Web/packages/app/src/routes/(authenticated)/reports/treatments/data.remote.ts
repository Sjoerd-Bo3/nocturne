/**
 * Remote functions for treatments report page.
 * Data comes from V4 decomposed endpoints (boluses, carb intakes, BG checks, notes, device events).
 */
import { z } from 'zod';
import { getRequestEvent, form, command, query } from '$app/server';
import { invalid } from '@sveltejs/kit';
import type { Bolus, CarbIntake, BGCheck, Note, DeviceEvent } from '$lib/api';
import { getProfileSummary } from '$api/generated/profiles.generated.remote';
import { getLocalDayBoundariesUtc } from '$lib/utils/timezone';

/**
 * Input schema for date range queries (matches reports layout pattern)
 */
const DateRangeSchema = z.object({
	days: z.number().nullish(),
	from: z.string().nullish(),
	to: z.string().nullish(),
});

function calculateDateRange(input: z.infer<typeof DateRangeSchema> | undefined, timezone?: string | null) {
	let startDateStr: string;
	let endDateStr: string;

	if (input?.from && input?.to) {
		startDateStr = input.from.split('T')[0];
		endDateStr = input.to.split('T')[0];
	} else if (input?.days) {
		const end = new Date();
		const start = new Date(end);
		start.setDate(end.getDate() - (input.days - 1));
		startDateStr = start.toISOString().split('T')[0];
		endDateStr = end.toISOString().split('T')[0];
	} else {
		const end = new Date();
		const start = new Date(end);
		start.setDate(end.getDate() - 7);
		startDateStr = start.toISOString().split('T')[0];
		endDateStr = end.toISOString().split('T')[0];
	}

	const { start: startDate } = getLocalDayBoundariesUtc(startDateStr, timezone);
	const { end: endDate } = getLocalDayBoundariesUtc(endDateStr, timezone);

	return { startDate, endDate };
}

/**
 * Get all v4 entry types for the treatments page.
 * Fetches boluses, carb intakes, BG checks, notes, and device events in parallel.
 * Treatment summary comes from the backend via calculateTreatmentSummary.
 */
export const getTreatmentsData = query(
	DateRangeSchema.optional(),
	async (input) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;
		const profile = await getProfileSummary(undefined);
		const timezone = profile?.therapySettings?.[0]?.timezone;
		const { startDate, endDate } = calculateDateRange(input, timezone);
		const [bolusResponse, carbResponse, bgCheckResponse, noteResponse, deviceEventResponse] =
			await Promise.all([
				apiClient.bolus.getAll(startDate, endDate, 10000),
				apiClient.nutrition.getCarbIntakes(startDate, endDate, 10000),
				apiClient.bGCheck.getAll(startDate, endDate, 10000),
				apiClient.note.getAll(startDate, endDate, 10000),
				apiClient.deviceEvent.getAll(startDate, endDate, 10000),
			]);

		const boluses = bolusResponse.data ?? [];
		const carbIntakes = carbResponse.data ?? [];
		const bgChecks = bgCheckResponse.data ?? [];
		const notes = noteResponse.data ?? [];
		const deviceEvents = deviceEventResponse.data ?? [];

		const treatmentSummary =
			boluses.length > 0 || carbIntakes.length > 0
				? await apiClient.statistics.calculateTreatmentSummary({ boluses, carbIntakes })
				: null;

		return {
			boluses,
			carbIntakes,
			bgChecks,
			notes,
			deviceEvents,
			treatmentSummary,
			dateRange: {
				from: startDate.toISOString(),
				to: endDate.toISOString(),
			},
		};
	}
);

/**
 * Delete a single entry form (v4: dispatches to the correct endpoint by kind)
 */
export const deleteEntryForm = form(
	z.object({
		entryId: z.string().min(1, 'Entry ID is required'),
		entryKind: z.enum(['bolus', 'carbs', 'bgCheck', 'note', 'deviceEvent']),
	}),
	async ({ entryId, entryKind }, issue) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		try {
			switch (entryKind) {
				case 'bolus':
					await apiClient.bolus.delete(entryId);
					break;
				case 'carbs':
					await apiClient.nutrition.deleteCarbIntake(entryId);
					break;
				case 'bgCheck':
					await apiClient.bGCheck.delete(entryId);
					break;
				case 'note':
					await apiClient.note.delete(entryId);
					break;
				case 'deviceEvent':
					await apiClient.deviceEvent.delete(entryId);
					break;
			}

			return {
				success: true,
				message: 'Entry deleted successfully',
				deletedEntryId: entryId,
			};
		} catch (error) {
			console.error('Error deleting entry:', error);
			invalid(issue.entryId('Failed to delete entry. Please try again.'));
		}
	}
);

/**
 * Bulk delete entries command (v4: dispatches each item by kind)
 */
export const bulkDeleteEntries = command(
	z.array(
		z.object({
			id: z.string(),
			kind: z.enum(['bolus', 'carbs', 'bgCheck', 'note', 'deviceEvent']),
		})
	),
	async (items) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		const deletedIds: string[] = [];
		const failedIds: string[] = [];

		for (const item of items) {
			try {
				switch (item.kind) {
					case 'bolus':
						await apiClient.bolus.delete(item.id);
						break;
					case 'carbs':
						await apiClient.nutrition.deleteCarbIntake(item.id);
						break;
					case 'bgCheck':
						await apiClient.bGCheck.delete(item.id);
						break;
					case 'note':
						await apiClient.note.delete(item.id);
						break;
					case 'deviceEvent':
						await apiClient.deviceEvent.delete(item.id);
						break;
				}
				deletedIds.push(item.id);
			} catch (err) {
				console.error(`Error deleting ${item.kind} ${item.id}:`, err);
				failedIds.push(item.id);
			}
		}

		if (failedIds.length > 0) {
			return {
				success: false,
				message: `Failed to delete ${failedIds.length} of ${items.length} entries`,
				deletedEntryIds: deletedIds,
				failedEntryIds: failedIds,
			};
		}

		return {
			success: true,
			message: `Successfully deleted ${deletedIds.length} entr${deletedIds.length !== 1 ? 'ies' : 'y'}`,
			deletedEntryIds: deletedIds,
		};
	}
);

/**
 * Update a single entry (v4: dispatches to the correct endpoint by kind)
 */
export const updateEntry = command(
	z.object({
		kind: z.enum(['bolus', 'carbs', 'bgCheck', 'note', 'deviceEvent']),
		id: z.string().min(1),
		data: z.record(z.string(), z.unknown()),
	}),
	async ({ kind, id, data }) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		switch (kind) {
			case 'bolus':
				return await apiClient.bolus.update(id, data as Bolus);
			case 'carbs':
				return await apiClient.nutrition.updateCarbIntake(id, data as CarbIntake);
			case 'bgCheck':
				return await apiClient.bGCheck.update(id, data as BGCheck);
			case 'note':
				return await apiClient.note.update(id, data as Note);
			case 'deviceEvent':
				return await apiClient.deviceEvent.update(id, data as DeviceEvent);
		}
	}
);

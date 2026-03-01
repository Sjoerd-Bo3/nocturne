/**
 * Compute UTC start/end of a local day for a given IANA timezone.
 * Falls back to UTC if timezone is not provided.
 */
export function getLocalDayBoundariesUtc(
	dateStr: string,
	timeZone?: string | null
): { start: Date; end: Date } {
	if (!timeZone) {
		const start = new Date(dateStr + 'T00:00:00Z');
		const end = new Date(dateStr + 'T23:59:59.999Z');
		return { start, end };
	}

	// Use Intl to determine the UTC offset at local midnight for the given timezone
	const utcMidnight = new Date(dateStr + 'T00:00:00Z');
	const utcStr = utcMidnight.toLocaleString('en-US', { timeZone: 'UTC' });
	const localStr = utcMidnight.toLocaleString('en-US', { timeZone });
	const offsetMs = new Date(localStr).getTime() - new Date(utcStr).getTime();

	// Local midnight = UTC midnight minus the timezone offset
	const start = new Date(utcMidnight.getTime() - offsetMs);
	const end = new Date(start.getTime() + 24 * 60 * 60 * 1000 - 1);
	return { start, end };
}

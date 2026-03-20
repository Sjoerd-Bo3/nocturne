import type { PageServerLoad } from "./$types";
import { redirect } from "@sveltejs/kit";

export const load: PageServerLoad = async ({ locals, url }) => {
  if (!locals.isAuthenticated) {
    const returnUrl = url.pathname + url.search;
    redirect(303, `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`);
  }

  const { apiClient } = locals;

  // Fetch 14-day daily summary for TDD average
  const now = new Date();
  const year = now.getFullYear();
  let avgTdd: number | null = null;

  try {
    const summary = await apiClient.dataOverview.getDailySummary(year);
    const fourteenDaysAgo = new Date(now);
    fourteenDaysAgo.setDate(fourteenDaysAgo.getDate() - 14);

    const recentDays =
      summary.days?.filter((d) => {
        if (!d.date) return false;
        const date = new Date(d.date);
        return date >= fourteenDaysAgo && date <= now;
      }) ?? [];

    const tdds = recentDays
      .map((d) => d.totalDailyDose)
      .filter((v): v is number => v != null && v > 0);

    if (tdds.length >= 2) {
      avgTdd = Math.round((tdds.reduce((a, b) => a + b, 0) / tdds.length) * 10) / 10;
    }
  } catch (err) {
    console.error("Error loading daily summary for packing hints:", err);
  }

  // Fetch 90-day device events for interval hints
  const ninetyDaysAgo = new Date(now);
  ninetyDaysAgo.setDate(ninetyDaysAgo.getDate() - 90);
  const eventIntervals: Record<string, number> = {};

  try {
    const events = await apiClient.deviceEvents.getAll(
      ninetyDaysAgo,
      now,
      500,
      0,
      "timestamp_asc"
    );

    // Group events by type and compute average intervals
    const eventsByType: Record<string, Date[]> = {};
    for (const event of events.data ?? []) {
      const eventType = event.eventType;
      if (!eventType || !event.timestamp) continue;
      if (!eventsByType[eventType]) eventsByType[eventType] = [];
      eventsByType[eventType].push(new Date(event.timestamp));
    }

    for (const [eventType, timestamps] of Object.entries(eventsByType)) {
      if (timestamps.length < 2) continue;
      const sorted = timestamps.sort((a, b) => a.getTime() - b.getTime());
      const intervals: number[] = [];
      for (let i = 1; i < sorted.length; i++) {
        const diffDays =
          (sorted[i].getTime() - sorted[i - 1].getTime()) / (1000 * 60 * 60 * 24);
        if (diffDays > 0.5) {
          // Ignore intervals under 12 hours (likely duplicates)
          intervals.push(diffDays);
        }
      }
      if (intervals.length > 0) {
        eventIntervals[eventType] =
          Math.round((intervals.reduce((a, b) => a + b, 0) / intervals.length) * 10) / 10;
      }
    }
  } catch (err) {
    console.error("Error loading device events for packing hints:", err);
  }

  return {
    avgTdd,
    eventIntervals,
  };
};

<script lang="ts">
  import { goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import { Chart, Calendar, Tooltip, Layer, Rect } from "layerchart";
  import { scaleLinear, scaleThreshold } from "d3-scale";
  import { timeWeek, timeMonths } from "d3-time";
  import {
    CalendarDays,
    X,
    ArrowRight,
    Loader2,
    Filter,
    SlidersHorizontal,
  } from "lucide-svelte";
  import * as Select from "$lib/components/ui/select";
  import { Button } from "$lib/components/ui/button";
  import { Separator } from "$lib/components/ui/separator";
  import * as Popover from "$lib/components/ui/popover";
  import { Checkbox } from "$lib/components/ui/checkbox";
  import {
    getAvailableYears,
    getDailySummary,
    getGriTimeline,
  } from "$api/generated/dataOverviews.generated.remote";
  import GlycemicRiskIndexChart from "$lib/components/reports/GlycemicRiskIndexChart.svelte";
  import type {
    DailySummaryDay,
    GriTimelinePeriod,
  } from "$api/generated/nocturne-api-client";
  import { getDataTypeLabel } from "$lib/utils/data-type-labels";
  import { formatGlucoseValue, getUnitLabel } from "$lib/utils/formatting";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import { getDateParamsContext } from "$lib/hooks/date-params.svelte";
  import { fly, fade, slide } from "svelte/transition";
  import { cubicOut } from "svelte/easing";

  const reportsParams = getDateParamsContext();

  // =========================================================================
  // State
  // =========================================================================

  let availableYears = $state<number[]>([]);
  let availableDataSources = $state<string[]>([]);
  let selectedDataSources = $state<string[]>([]);
  let prevDataSources = $state<string[]>([]);
  let yearData = $state<Map<number, DailySummaryDay[]>>(new Map());
  let griTimelineData = $state<Map<number, GriTimelinePeriod[]>>(new Map());
  let loadingYears = $state<Set<number>>(new Set());
  let metadataLoaded = $state(false);
  let metadataLoading = $state(false);
  let selectedDay = $state<CalendarDatum | null>(null);
  let sentinelElements: Record<number, HTMLDivElement | undefined> = $state({});

  type HeatmapMetric = "avgGlucose" | "tir" | "bolus" | "basal" | "tdd" | "carbs";

  const METRIC_OPTIONS: { value: HeatmapMetric; label: string }[] = [
    { value: "avgGlucose", label: "Avg Glucose" },
    { value: "tir", label: "Time in Range" },
    { value: "bolus", label: "Bolus" },
    { value: "basal", label: "Basal" },
    { value: "tdd", label: "TDD" },
    { value: "carbs", label: "Carbs" },
  ];

  let selectedMetric = $state<HeatmapMetric>("avgGlucose");

  /** All known data types that can appear in counts */
  const ALL_DATA_TYPES = [
    "Glucose",
    "ManualBG",
    "Boluses",
    "CarbIntake",
    "BolusCalculations",
    "Notes",
    "DeviceEvents",
    "StateSpans",
    "Activity",
    "DeviceStatus",
  ];

  /** Data types currently hidden by the filter */
  let hiddenDataTypes = $state<Set<string>>(new Set());

  // =========================================================================
  // Glucose color scale
  // =========================================================================

  const glucoseColorScale = scaleThreshold<number, string>()
    .domain([54, 70, 180, 250])
    .range([
      "var(--glucose-very-low)",
      "var(--glucose-low)",
      "var(--glucose-in-range)",
      "var(--glucose-high)",
      "var(--glucose-very-high)",
    ]);

  /** Multi-hue heatmap scale — maximises perceptual distinction in the 70–250 range */
  const HEATMAP_DOMAIN = [40, 54, 70, 100, 140, 180, 220, 260, 350];
  const HEATMAP_COLORS = [
    "#2563eb", // blue-600   — critically low
    "#3b82f6", // blue-500   — very low
    "#06b6d4", // cyan-500   — low
    "#10b981", // emerald-500 — on target
    "#84cc16", // lime-500   — upper in-range
    "#eab308", // yellow-500  — entering high
    "#f97316", // orange-500  — high
    "#ef4444", // red-500    — very high
    "#b91c1c", // red-700    — critically high
  ];

  const heatmapScale = scaleLinear<string>()
    .domain(HEATMAP_DOMAIN)
    .range(HEATMAP_COLORS)
    .clamp(true);

  const LEGEND_W = 420;
  const LEGEND_THRESHOLDS = [70, 180, 250];

  function legendX(mgdl: number): number {
    return ((mgdl - 40) / 310) * LEGEND_W;
  }

  /** CSS variable names for each metric's hue */
  const METRIC_CSS_VARS: Record<Exclude<HeatmapMetric, "avgGlucose">, string> = {
    tir: "--chart-2",
    bolus: "--chart-1",
    basal: "--chart-3",
    tdd: "--chart-4",
    carbs: "--chart-5",
  };

  /** Compute max value for a metric across all loaded year data */
  function getMetricMax(metric: HeatmapMetric): number {
    let max = 0;
    for (const days of yearData.values()) {
      for (const day of days) {
        let val: number | undefined | null;
        switch (metric) {
          case "bolus": val = day.totalBolusUnits; break;
          case "basal": val = day.totalBasalUnits; break;
          case "tdd": val = day.totalDailyDose; break;
          case "carbs": val = day.totalCarbs; break;
          case "tir": val = day.timeInRangePercent; break;
          default: val = day.averageGlucoseMgdl; break;
        }
        if (val != null && val > max) max = val;
      }
    }
    return max || 1;
  }

  /** Memoized max for current metric — recomputed only when metric or data changes */
  const metricMaxCached = $derived.by(() => {
    // Depend on yearData and selectedMetric
    void yearData;
    if (selectedMetric === "avgGlucose") return 1;
    if (selectedMetric === "tir") return 100;
    return getMetricMax(selectedMetric);
  });

  /** Get cell value for the selected metric */
  function getMetricCellValue(data: CalendarDatum): number | null {
    switch (selectedMetric) {
      case "tir": return data.timeInRangePercent;
      case "bolus": return data.totalBolusUnits;
      case "basal": return data.totalBasalUnits;
      case "tdd": return data.totalDailyDose;
      case "carbs": return data.totalCarbs;
      default: return null;
    }
  }

  function getIntensityFill(value: number, maxVal: number, cssVarName: string): string {
    const intensity = Math.min(value / maxVal, 1);
    // Scale from 15% opacity (min visible) to 100%
    const alpha = 0.15 + intensity * 0.85;
    return `color-mix(in srgb, var(${cssVarName}) ${Math.round(alpha * 100)}%, transparent)`;
  }

  function getCellFill(data: CalendarDatum | undefined): string {
    if (!data) return "rgb(0 0 0 / 5%)";

    if (selectedMetric === "avgGlucose") {
      if (data.value != null) return heatmapScale(data.value);
      if (data.filteredCount > 0) return "hsl(var(--muted))";
      return "rgb(0 0 0 / 5%)";
    }

    const metricValue = getMetricCellValue(data);
    if (metricValue == null) {
      if (data.filteredCount > 0) return "hsl(var(--muted))";
      return "rgb(0 0 0 / 5%)";
    }

    const maxVal = metricMaxCached;
    const cssVar = METRIC_CSS_VARS[selectedMetric as Exclude<HeatmapMetric, "avgGlucose">];
    return getIntensityFill(metricValue, maxVal, cssVar);
  }

  // =========================================================================
  // Derived
  // =========================================================================

  const units = $derived(glucoseUnits.current);
  const unitLabel = $derived(getUnitLabel(units));
  const sortedYears = $derived([...availableYears].sort((a, b) => b - a));

  /** Discover data types present in loaded data */
  const presentDataTypes = $derived.by(() => {
    const types = new Set<string>();
    for (const days of yearData.values()) {
      for (const day of days) {
        if (day.counts) {
          for (const key of Object.keys(day.counts) as string[]) {
            types.add(key);
          }
        }
      }
    }
    return ALL_DATA_TYPES.filter((t) => types.has(t));
  });

  // =========================================================================
  // Data Loading
  // =========================================================================

  async function loadMetadata() {
    if (metadataLoading) return;
    metadataLoading = true;
    try {
      const result = getAvailableYears();
      await waitForQuery(result);
      availableYears = result.current?.years ?? [];
      availableDataSources = result.current?.availableDataSources ?? [];
      metadataLoaded = true;
    } catch (err) {
      console.error("Failed to load available years:", err);
    } finally {
      metadataLoading = false;
    }
  }

  function waitForQuery<T>(query: {
    loading: boolean;
    current: T | undefined;
    error: unknown;
  }): Promise<T> {
    return new Promise((resolve, reject) => {
      if (!query.loading && query.current !== undefined) {
        resolve(query.current);
        return;
      }
      if (!query.loading && query.error) {
        reject(query.error);
        return;
      }
      const interval = setInterval(() => {
        if (!query.loading && query.current !== undefined) {
          clearInterval(interval);
          resolve(query.current);
        } else if (!query.loading && query.error) {
          clearInterval(interval);
          reject(query.error);
        }
      }, 50);
      setTimeout(() => {
        clearInterval(interval);
        reject(new Error("Query timed out"));
      }, 30000);
    });
  }

  async function loadYearData(year: number) {
    if (loadingYears.has(year) || yearData.has(year)) return;

    loadingYears = new Set([...loadingYears, year]);
    try {
      const params: { year: number; dataSources?: string[] } = { year };
      if (selectedDataSources.length > 0) {
        params.dataSources = selectedDataSources;
      }
      const result = getDailySummary(params);
      await waitForQuery(result);
      const days = result.current?.days ?? [];
      yearData = new Map([...yearData, [year, days]]);
      loadGriTimeline(year);
    } catch (err) {
      console.error(`Failed to load data for year ${year}:`, err);
    } finally {
      const next = new Set(loadingYears);
      next.delete(year);
      loadingYears = next;
    }
  }

  async function loadGriTimeline(year: number) {
    if (griTimelineData.has(year)) return;
    try {
      const result = getGriTimeline({
        year,
        dataSources:
          selectedDataSources.length > 0 ? selectedDataSources : undefined,
      });
      await waitForQuery(result);
      const periods = result.current?.periods ?? [];
      griTimelineData = new Map([...griTimelineData, [year, periods]]);
    } catch (err) {
      console.error(`Failed to load GRI timeline for year ${year}:`, err);
    }
  }

  function clearAndReload() {
    yearData = new Map();
    griTimelineData = new Map();
    loadingYears = new Set();
    if (sortedYears.length > 0) {
      loadYearData(sortedYears[0]);
    }
  }

  // =========================================================================
  // Chart data transformation
  // =========================================================================

  type CalendarDatum = {
    date: Date;
    value: number | null;
    totalCount: number;
    filteredCount: number;
    averageGlucoseMgdl: number | null;
    totalBolusUnits: number | null;
    totalBasalUnits: number | null;
    totalDailyDose: number | null;
    totalCarbs: number | null;
    timeInRangePercent: number | null;
    counts: Record<string, number>;
    dateString: string;
  };

  function transformYearData(days: DailySummaryDay[]): CalendarDatum[] {
    return days.map((day) => {
      const dateStr = day.date ?? "";
      const [y, m, d] = dateStr.split("-").map(Number);
      const date = new Date(y, m - 1, d);
      const avg = day.averageGlucoseMgdl ?? null;
      const counts = (day.counts as Record<string, number>) ?? {};

      // Calculate filtered count excluding hidden types
      const filteredCount = Object.entries(counts)
        .filter(([key]) => !hiddenDataTypes.has(key))
        .reduce((sum, [, count]) => sum + count, 0);

      return {
        date,
        value: avg,
        totalCount: day.totalCount ?? 0,
        filteredCount,
        averageGlucoseMgdl: avg,
        totalBolusUnits: day.totalBolusUnits ?? null,
        totalBasalUnits: day.totalBasalUnits ?? null,
        totalDailyDose: day.totalDailyDose ?? null,
        totalCarbs: day.totalCarbs ?? null,
        timeInRangePercent: day.timeInRangePercent ?? null,
        counts,
        dateString: dateStr,
      };
    });
  }

  // =========================================================================
  // Data type filter
  // =========================================================================

  function toggleDataType(dataType: string) {
    const next = new Set(hiddenDataTypes);
    if (next.has(dataType)) {
      next.delete(dataType);
    } else {
      next.add(dataType);
    }
    hiddenDataTypes = next;
  }

  function showAllDataTypes() {
    hiddenDataTypes = new Set();
  }

  // =========================================================================
  // IntersectionObserver for lazy loading
  // =========================================================================

  let observer: IntersectionObserver | undefined;

  function setupObserver() {
    if (!browser) return;

    observer?.disconnect();
    observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            const year = Number((entry.target as HTMLElement).dataset.year);
            if (!isNaN(year)) {
              loadYearData(year);
            }
          }
        }
      },
      { rootMargin: "200px" }
    );

    for (const year of sortedYears) {
      const el = sentinelElements[year];
      if (el) observer.observe(el);
    }
  }

  // =========================================================================
  // Day detail panel
  // =========================================================================

  function closeDetailPanel() {
    selectedDay = null;
  }

  function navigateToDayInReview(dateStr: string) {
    if (reportsParams) {
      reportsParams.setCustomRange(dateStr, dateStr);
    }
    goto(
      `/reports/day-in-review?from=${dateStr}&to=${dateStr}&isDefault=false`
    );
  }

  // =========================================================================
  // Lifecycle
  // =========================================================================

  $effect(() => {
    if (browser && !metadataLoaded && !metadataLoading) {
      loadMetadata();
    }
  });

  $effect(() => {
    if (metadataLoaded && sortedYears.length > 0) {
      loadYearData(sortedYears[0]);
    }
  });

  $effect(() => {
    void sentinelElements;
    if (browser && metadataLoaded) {
      setupObserver();
    }
    return () => {
      observer?.disconnect();
    };
  });

  // Re-fetch when data source filter changes
  $effect(() => {
    const currentKey = selectedDataSources.sort().join(",");
    const prevKey = prevDataSources.sort().join(",");
    if (currentKey !== prevKey && metadataLoaded) {
      prevDataSources = [...selectedDataSources];
      clearAndReload();
    }
  });

  // =========================================================================
  // Helpers
  // =========================================================================

  function getYearBounds(year: number): { start: Date; end: Date } {
    return {
      start: new Date(year, 0, 1),
      end: new Date(year, 11, 31),
    };
  }

  function formatSelectedDate(dateStr: string): string {
    const [y, m, d] = dateStr.split("-").map(Number);
    const date = new Date(y, m - 1, d);
    return date.toLocaleDateString(undefined, {
      weekday: "long",
      year: "numeric",
      month: "long",
      day: "numeric",
    });
  }

  function formatUnits(value: number | null): string {
    if (value == null) return "-";
    return value.toFixed(1) + " U";
  }

  /** Get visible counts (filtered by hiddenDataTypes) */
  function getVisibleCounts(
    counts: Record<string, number>
  ): [string, number][] {
    return Object.entries(counts)
      .filter(([key, count]) => count > 0 && !hiddenDataTypes.has(key))
      .sort(([, a], [, b]) => b - a);
  }

  /** Get ISO week number for a date */
  function getISOWeekNumber(date: Date): number {
    const d = new Date(
      Date.UTC(date.getFullYear(), date.getMonth(), date.getDate())
    );
    d.setUTCDate(d.getUTCDate() + 4 - (d.getUTCDay() || 7));
    const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
    return Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
  }

  /** Get the Monday and Sunday of the ISO week containing the given date */
  function getWeekBounds(date: Date): { from: string; to: string } {
    const d = new Date(date.getFullYear(), date.getMonth(), date.getDate());
    const day = d.getDay();
    const diffToMonday = day === 0 ? -6 : 1 - day;
    const monday = new Date(d);
    monday.setDate(d.getDate() + diffToMonday);
    const sunday = new Date(monday);
    sunday.setDate(monday.getDate() + 6);
    const fmt = (dt: Date) =>
      `${dt.getFullYear()}-${String(dt.getMonth() + 1).padStart(2, "0")}-${String(dt.getDate()).padStart(2, "0")}`;
    return { from: fmt(monday), to: fmt(sunday) };
  }

  type WeekColumn = {
    x: number;
    weekNumber: number;
    from: string;
    to: string;
  };

  /** Extract unique week columns from calendar cells */
  function getWeekColumns(
    cells: Array<{ x: number; data?: { date?: Date } }>
  ): WeekColumn[] {
    const seen = new Map<number, { date: Date }>();
    for (const cell of cells) {
      const date = cell.data?.date;
      if (date && !seen.has(cell.x)) {
        seen.set(cell.x, { date });
      }
    }
    return [...seen.entries()]
      .map(([x, { date }]) => ({
        x,
        weekNumber: getISOWeekNumber(date),
        ...getWeekBounds(date),
      }))
      .sort((a, b) => a.x - b.x);
  }
</script>

<svelte:head>
  <title>Year Overview - Nocturne</title>
  <meta
    name="description"
    content="Multi-year heatmap overview of all your diabetes data"
  />
</svelte:head>

<div class="flex min-h-full">
  <!-- Main Content -->
  <div
    class="flex-1 transition-[margin] duration-200 {selectedDay
      ? 'mr-80 lg:mr-96'
      : ''}"
  >
    <!-- Header -->
    <div
      class="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between"
    >
      <div class="flex items-center gap-3">
        <div
          class="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10"
        >
          <CalendarDays class="h-5 w-5 text-primary" />
        </div>
        <div>
          <h1 class="text-2xl font-bold tracking-tight">Year Overview</h1>
          <p class="text-sm text-muted-foreground">
            Multi-year heatmap of all your data
          </p>
        </div>
      </div>

      <!-- Filters -->
      <div class="flex items-center gap-2">
        <!-- Data Source Filter (multi-select) -->
        <div class="flex items-center gap-2">
          <Filter class="h-4 w-4 text-muted-foreground" />
          <Select.Root
            type="multiple"
            value={selectedDataSources}
            onValueChange={(v) => {
              selectedDataSources = v ?? [];
            }}
            disabled={availableDataSources.length === 0}
          >
            <Select.Trigger class="w-[200px]">
              <span class="truncate">
                {selectedDataSources.length === 0
                  ? "All Data Sources"
                  : selectedDataSources.length === 1
                    ? getDataTypeLabel(selectedDataSources[0])
                    : `${selectedDataSources.length} sources`}
              </span>
            </Select.Trigger>
            <Select.Content>
              {#each availableDataSources as source}
                <Select.Item value={source}>
                  {getDataTypeLabel(source)}
                </Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
        </div>

        <!-- Data Type Filter -->
        {#if presentDataTypes.length > 0}
          <Popover.Root>
            <Popover.Trigger>
              {#snippet child({ props })}
                <Button variant="outline" size="sm" class="gap-1.5" {...props}>
                  <SlidersHorizontal class="h-3.5 w-3.5" />
                  Types
                  {#if hiddenDataTypes.size > 0}
                    <span
                      class="ml-1 rounded-full bg-primary px-1.5 py-0.5 text-[10px] font-medium leading-none text-primary-foreground"
                    >
                      {presentDataTypes.length -
                        hiddenDataTypes.size}/{presentDataTypes.length}
                    </span>
                  {/if}
                </Button>
              {/snippet}
            </Popover.Trigger>
            <Popover.Content class="w-56 p-3" align="end">
              <div class="mb-2 flex items-center justify-between">
                <span class="text-xs font-medium text-muted-foreground">
                  Show data types
                </span>
                {#if hiddenDataTypes.size > 0}
                  <button
                    class="text-xs text-primary hover:underline"
                    onclick={showAllDataTypes}
                  >
                    Show all
                  </button>
                {/if}
              </div>
              <div class="space-y-1.5">
                {#each presentDataTypes as dataType}
                  <label
                    class="flex cursor-pointer items-center gap-2 rounded px-1 py-0.5 text-sm hover:bg-muted/50"
                  >
                    <Checkbox
                      checked={!hiddenDataTypes.has(dataType)}
                      onCheckedChange={() => toggleDataType(dataType)}
                    />
                    {getDataTypeLabel(dataType)}
                  </label>
                {/each}
              </div>
            </Popover.Content>
          </Popover.Root>
        {/if}
      </div>
    </div>

    <!-- Color Legend -->
    <div class="mb-6 rounded-lg border border-border bg-card p-3">
      {#if selectedMetric === "avgGlucose"}
        <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
          <Select.Root
            type="single"
            value={selectedMetric}
            onValueChange={(v) => {
              if (v) selectedMetric = v as HeatmapMetric;
            }}
          >
            <Select.Trigger class="w-[150px] h-8 text-xs">
              <span class="truncate">
                {METRIC_OPTIONS.find((o) => o.value === selectedMetric)?.label ?? "Avg Glucose"}
              </span>
            </Select.Trigger>
            <Select.Content>
              {#each METRIC_OPTIONS as option}
                <Select.Item value={option.value}>
                  {option.label}
                </Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
          <svg
            viewBox="0 0 {LEGEND_W} 48"
            class="h-12 w-full max-w-[420px] text-muted-foreground"
            overflow="visible"
            role="img"
            aria-label="Glucose color scale legend"
          >
            <defs>
              <linearGradient id="heatmap-grad">
                {#each HEATMAP_DOMAIN as v, i}
                  <stop
                    offset="{((v - 40) / 310) * 100}%"
                    stop-color={HEATMAP_COLORS[i]}
                  />
                {/each}
              </linearGradient>
            </defs>

            <!-- Zone labels -->
            <text x={legendX(55)} y="10" text-anchor="middle" font-size="10" fill="currentColor">Low</text>
            <text x={legendX(125)} y="10" text-anchor="middle" font-size="10" fill="currentColor">In Range</text>
            <text x={legendX(215)} y="10" text-anchor="middle" font-size="10" fill="currentColor">High</text>
            <text x={legendX(300)} y="10" text-anchor="middle" font-size="10" fill="currentColor">Very High</text>

            <!-- Gradient bar -->
            <rect x="0" y="14" width={LEGEND_W} height="14" rx="2" fill="url(#heatmap-grad)" />

            <!-- Threshold markers -->
            {#each LEGEND_THRESHOLDS as threshold}
              {@const x = legendX(threshold)}
              <line
                x1={x}
                y1={14}
                x2={x}
                y2={32}
                stroke="currentColor"
                stroke-opacity="0.3"
              />
              <text
                {x}
                y={44}
                text-anchor="middle"
                font-size="10"
                fill="currentColor"
              >
                {formatGlucoseValue(threshold, units)}
              </text>
            {/each}
          </svg>
          <div class="flex items-center gap-1.5 text-xs text-muted-foreground">
            <span
              class="inline-block h-3 w-3 rounded-sm"
              style="background: hsl(var(--muted))"
            ></span>
            Other Data (no glucose)
          </div>
        </div>
      {:else}
        <!-- Single-hue legend for other metrics -->
        {@const metricLabel = METRIC_OPTIONS.find(o => o.value === selectedMetric)?.label ?? ""}
        {@const metricUnit = selectedMetric === "tir" ? "%" : selectedMetric === "carbs" ? "g" : "U"}
        {@const metricMax = selectedMetric === "tir" ? 100 : getMetricMax(selectedMetric)}
        {@const cssVar = METRIC_CSS_VARS[selectedMetric as Exclude<HeatmapMetric, "avgGlucose">]}
        <div class="flex flex-wrap items-center gap-x-4 gap-y-2">
          <Select.Root
            type="single"
            value={selectedMetric}
            onValueChange={(v) => {
              if (v) selectedMetric = v as HeatmapMetric;
            }}
          >
            <Select.Trigger class="w-[150px] h-8 text-xs">
              <span class="truncate">
                {METRIC_OPTIONS.find((o) => o.value === selectedMetric)?.label ?? "Avg Glucose"}
              </span>
            </Select.Trigger>
            <Select.Content>
              {#each METRIC_OPTIONS as option}
                <Select.Item value={option.value}>
                  {option.label}
                </Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
          <svg
            viewBox="0 0 {LEGEND_W} 36"
            class="h-9 w-full max-w-[420px] text-muted-foreground"
            overflow="visible"
            role="img"
            aria-label="{metricLabel} color scale legend"
          >
            <defs>
              <linearGradient id="metric-grad">
                <stop offset="0%" stop-color="var({cssVar})" stop-opacity="0.15" />
                <stop offset="100%" stop-color="var({cssVar})" stop-opacity="1" />
              </linearGradient>
            </defs>
            <rect x="0" y="4" width={LEGEND_W} height="14" rx="2" fill="url(#metric-grad)" />
            <text x="0" y="32" font-size="10" fill="currentColor">0</text>
            <text x={LEGEND_W} y="32" text-anchor="end" font-size="10" fill="currentColor">
              {selectedMetric === "tir" ? "100%" : `${Math.round(metricMax)} ${metricUnit}`}
            </text>
            {#if selectedMetric === "tir"}
              <!-- 70% TIR target marker -->
              {@const targetX = (70 / 100) * LEGEND_W}
              <line x1={targetX} y1={4} x2={targetX} y2={18} stroke="currentColor" stroke-opacity="0.3" />
              <text x={targetX} y="32" text-anchor="middle" font-size="10" fill="currentColor">70%</text>
            {/if}
          </svg>
          <div class="flex items-center gap-1.5 text-xs text-muted-foreground">
            <span class="inline-block h-3 w-3 rounded-sm" style="background: hsl(var(--muted))"></span>
            No {metricLabel.toLowerCase()} data
          </div>
        </div>
      {/if}
    </div>

    <!-- Loading state for metadata -->
    {#if metadataLoading && !metadataLoaded}
      <div
        class="flex items-center justify-center py-20"
        in:fade={{ duration: 200 }}
      >
        <div class="flex flex-col items-center gap-3">
          <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
          <p class="text-sm text-muted-foreground">Loading data overview...</p>
        </div>
      </div>
    {/if}

    <!-- No data state -->
    {#if metadataLoaded && sortedYears.length === 0}
      <div
        class="flex items-center justify-center py-20"
        in:fade={{ duration: 300 }}
      >
        <div class="max-w-md space-y-4 text-center">
          <div
            class="mx-auto flex h-16 w-16 items-center justify-center rounded-full bg-muted"
          >
            <CalendarDays class="h-8 w-8 text-muted-foreground" />
          </div>
          <h2 class="text-xl font-semibold">No Data Available</h2>
          <p class="text-muted-foreground">
            There is no data to display yet. Connect a data source in your
            settings to get started.
          </p>
          <Button href="/settings/connectors" variant="outline">
            Configure Data Sources
          </Button>
        </div>
      </div>
    {/if}

    <!-- Year Calendars -->
    {#if metadataLoaded && sortedYears.length > 0}
      <div class="space-y-10">
        {#each sortedYears as year, yearIndex (year)}
          {@const bounds = getYearBounds(year)}
          {@const days = yearData.get(year)}
          {@const chartData = days ? transformYearData(days) : []}
          {@const isYearLoading = loadingYears.has(year) && !days}

          <div
            in:fly={{
              y: 30,
              duration: 500,
              delay: Math.min(yearIndex * 100, 300),
              easing: cubicOut,
            }}
          >
            <!-- Sentinel for IntersectionObserver -->
            <div
              data-year={year}
              bind:this={sentinelElements[year]}
              class="pointer-events-none h-0"
            ></div>

            <!-- Year Label -->
            <div class="mb-3 flex items-center gap-3">
              <h2 class="text-xl font-bold tabular-nums">{year}</h2>
              {#if isYearLoading}
                <Loader2 class="h-4 w-4 animate-spin text-muted-foreground" />
              {/if}
              {#if days}
                <span class="text-sm text-muted-foreground">
                  {days.filter((d) => (d.totalCount ?? 0) > 0).length} days with data
                </span>
              {/if}
            </div>

            <!-- Calendar Heatmap -->
            {#if chartData.length > 0}
              <div
                class="w-full overflow-x-clip overflow-y-visible rounded-lg border border-border bg-card p-4"
              >
                <div class="min-w-[900px] h-60">
                  <Chart
                    data={chartData}
                    x="date"
                    c="value"
                    cScale={scaleThreshold().unknown("transparent")}
                    cDomain={[54, 70, 180, 250]}
                    cRange={[
                      "var(--glucose-very-low)",
                      "var(--glucose-low)",
                      "var(--glucose-in-range)",
                      "var(--glucose-high)",
                      "var(--glucose-very-high)",
                    ]}
                    tooltip={{ mode: "manual" }}
                  >
                    {#snippet children({ context })}
                      <Layer type="svg">
                        <Calendar
                          start={bounds.start}
                          end={bounds.end}
                          cellSize={24}
                          monthPath
                          monthLabel={false}
                          tooltipContext={context.tooltip}
                        >
                          {#snippet children({ cells, cellSize })}
                            <!-- Month labels (clickable → calendar) -->
                            {#each timeMonths(bounds.start, bounds.end) as monthDate}
                              {@const monthX =
                                timeWeek.count(
                                  bounds.start,
                                  timeWeek.ceil(monthDate)
                                ) * cellSize[0]}
                              <a
                                href="/calendar?year={monthDate.getFullYear()}&month={monthDate.getMonth() +
                                  1}"
                              >
                                <text
                                  x={monthX}
                                  y={-5}
                                  font-size="12"
                                  class="fill-muted-foreground hover:fill-primary cursor-pointer"
                                >
                                  {monthDate.toLocaleString(undefined, {
                                    month: "short",
                                  })}
                                </text>
                              </a>
                            {/each}
                            {#each cells as cell}
                              {@const padding = 1}
                              {@const cellDate = cell.data?.dateString}
                              <Rect
                                x={cell.x + padding}
                                y={cell.y + padding}
                                width={cellSize[0] - padding * 2}
                                height={cellSize[1] - padding * 2}
                                rx={4}
                                fill={getCellFill(cell.data)}
                                onpointermove={(e) =>
                                  context.tooltip?.show(e, cell.data)}
                                onpointerleave={() => context.tooltip?.hide()}
                                onclick={() => {
                                  if (cellDate) {
                                    navigateToDayInReview(cellDate);
                                  }
                                }}
                              />
                            {/each}
                            <!-- Week number labels -->
                            {@const weekCols = getWeekColumns(cells)}
                            {#each weekCols as wk}
                              <a
                                href="/reports/week-to-week?from={wk.from}&to={wk.to}&isDefault=false"
                              >
                                <text
                                  x={wk.x + cellSize[0] / 2}
                                  y={7 * cellSize[1] + 14}
                                  text-anchor="middle"
                                  font-size="9"
                                  class="fill-muted-foreground hover:fill-primary cursor-pointer"
                                >
                                  {wk.weekNumber}
                                </text>
                              </a>
                            {/each}
                          {/snippet}
                        </Calendar>
                      </Layer>

                      <Tooltip.Root
                        class="rounded-md border bg-popover p-2.5 text-popover-foreground shadow-md"
                      >
                        {#snippet children({ data })}
                          {@const d = data as CalendarDatum}
                          {#if d?.dateString}
                            <div class="text-xs min-w-40">
                              <!-- Date header -->
                              <div class="mb-1.5 font-semibold">
                                {d.date.toLocaleDateString(undefined, {
                                  weekday: "short",
                                  month: "short",
                                  day: "numeric",
                                })}
                              </div>

                              <!-- Average glucose -->
                              {#if d.averageGlucoseMgdl != null}
                                <div class="mb-2 flex items-baseline gap-1.5">
                                  <span
                                    class="text-sm font-bold tabular-nums"
                                    style="color: {glucoseColorScale(
                                      d.averageGlucoseMgdl
                                    )}"
                                  >
                                    {formatGlucoseValue(
                                      d.averageGlucoseMgdl,
                                      units
                                    )}
                                  </span>
                                  <span class="text-muted-foreground">
                                    avg {unitLabel}
                                  </span>
                                </div>
                              {/if}

                              <!-- Insulin & Carbs summary -->
                              {#if d.totalDailyDose != null || d.totalCarbs != null}
                                <div
                                  class="mb-2 space-y-0.5 border-t border-border/50 pt-1.5"
                                >
                                  {#if d.totalDailyDose != null}
                                    <div
                                      class="text-[10px] font-medium uppercase tracking-wider text-muted-foreground"
                                    >
                                      Insulin
                                    </div>
                                    <div class="flex justify-between gap-4">
                                      <span class="text-muted-foreground">
                                        Bolus
                                      </span>
                                      <span class="font-medium tabular-nums">
                                        {formatUnits(d.totalBolusUnits)}
                                      </span>
                                    </div>
                                    <div class="flex justify-between gap-4">
                                      <span class="text-muted-foreground">
                                        Basal
                                      </span>
                                      <span class="font-medium tabular-nums">
                                        {formatUnits(d.totalBasalUnits)}
                                      </span>
                                    </div>
                                    <div
                                      class="flex justify-between gap-4 border-t border-border/30 pt-0.5"
                                    >
                                      <span class="text-muted-foreground">
                                        TDD
                                      </span>
                                      <span class="font-semibold tabular-nums">
                                        {formatUnits(d.totalDailyDose)}
                                      </span>
                                    </div>
                                  {/if}
                                  {#if d.totalCarbs != null}
                                    <div
                                      class="flex justify-between gap-4 {d.totalDailyDose !=
                                      null
                                        ? 'border-t border-border/30 pt-0.5'
                                        : ''}"
                                    >
                                      <span class="text-muted-foreground">
                                        Carbs
                                      </span>
                                      <span class="font-medium tabular-nums">
                                        {d.totalCarbs.toFixed(0)}g
                                      </span>
                                    </div>
                                  {/if}
                                </div>
                              {/if}

                              <!-- Record counts -->
                              {#if getVisibleCounts(d.counts).length > 0}
                                {@const visibleCounts = getVisibleCounts(
                                  d.counts
                                )}
                                <div
                                  class="space-y-0.5 border-t border-border/50 pt-1.5"
                                >
                                  <div
                                    class="text-[10px] font-medium uppercase tracking-wider text-muted-foreground"
                                  >
                                    Counts
                                  </div>
                                  {#each visibleCounts as [key, count]}
                                    <div class="flex justify-between gap-4">
                                      <span class="text-muted-foreground">
                                        {getDataTypeLabel(key)}
                                      </span>
                                      <span class="font-medium tabular-nums">
                                        {count}
                                      </span>
                                    </div>
                                  {/each}
                                </div>
                              {/if}
                            </div>
                          {/if}
                        {/snippet}
                      </Tooltip.Root>
                    {/snippet}
                  </Chart>
                </div>
              </div>
              {@const griPeriods = griTimelineData.get(year) ?? []}
              {#if griPeriods.length > 1}
                <div class="mt-4 rounded-lg border border-border bg-card p-4">
                  <GlycemicRiskIndexChart
                    gri={griPeriods[griPeriods.length - 1]?.gri ?? { score: 0 }}
                    timeSeriesData={griPeriods}
                  />
                </div>
              {/if}
            {:else if isYearLoading}
              <div
                class="flex h-[120px] items-center justify-center rounded-lg border border-border bg-card"
              >
                <div
                  class="flex items-center gap-2 text-sm text-muted-foreground"
                >
                  <Loader2 class="h-4 w-4 animate-spin" />
                  Loading {year} data...
                </div>
              </div>
            {:else}
              <div
                class="flex h-[120px] items-center justify-center rounded-lg border border-dashed border-border bg-card/50"
              >
                <p class="text-sm text-muted-foreground">
                  No data for {year}
                </p>
              </div>
            {/if}
          </div>
        {/each}
      </div>
    {/if}
  </div>

  <!-- Day Detail Panel -->
  {#if selectedDay}
    <div
      class="fixed right-0 top-14 z-30 flex h-[calc(100vh-3.5rem)] w-80 flex-col border-l border-border bg-card shadow-lg lg:w-96"
      transition:slide={{ axis: "x", duration: 200, easing: cubicOut }}
    >
      <!-- Panel Header -->
      <div
        class="flex items-center justify-between border-b border-border px-4 py-3"
      >
        <h3 class="text-sm font-semibold">Day Details</h3>
        <Button variant="ghost" size="icon" onclick={closeDetailPanel}>
          <X class="h-4 w-4" />
        </Button>
      </div>

      <!-- Panel Content -->
      <div class="flex-1 overflow-y-auto px-4 py-4">
        <!-- Date -->
        <div class="mb-4">
          <h4 class="text-lg font-semibold">
            {formatSelectedDate(selectedDay.dateString)}
          </h4>
        </div>

        <Separator class="mb-4" />

        <!-- Average Glucose -->
        {#if selectedDay.averageGlucoseMgdl != null}
          <div class="mb-4">
            <div
              class="text-xs font-medium uppercase tracking-wide text-muted-foreground"
            >
              Average Glucose
            </div>
            <div class="mt-1 text-2xl font-bold tabular-nums">
              <span
                style="color: {glucoseColorScale(
                  selectedDay.averageGlucoseMgdl
                )}"
              >
                {formatGlucoseValue(selectedDay.averageGlucoseMgdl, units)}
              </span>
              <span class="text-sm font-normal text-muted-foreground">
                {unitLabel}
              </span>
            </div>
          </div>
          <Separator class="mb-4" />
        {/if}

        <!-- Insulin & Carbs Summary -->
        {#if selectedDay.totalDailyDose != null || selectedDay.totalCarbs != null}
          <div class="mb-4">
            {#if selectedDay.totalDailyDose != null}
              <div
                class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground"
              >
                Insulin
              </div>
              <div class="space-y-2">
                <div
                  class="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-sm"
                >
                  <span>Bolus</span>
                  <span class="font-medium tabular-nums">
                    {formatUnits(selectedDay.totalBolusUnits)}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-sm"
                >
                  <span>Basal</span>
                  <span class="font-medium tabular-nums">
                    {formatUnits(selectedDay.totalBasalUnits)}
                  </span>
                </div>
                <div
                  class="flex items-center justify-between rounded-md bg-primary/10 px-3 py-2 text-sm font-medium"
                >
                  <span>Total Daily Dose</span>
                  <span class="tabular-nums">
                    {formatUnits(selectedDay.totalDailyDose)}
                  </span>
                </div>
              </div>
            {/if}
            {#if selectedDay.totalCarbs != null}
              <div
                class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground {selectedDay.totalDailyDose !=
                null
                  ? 'mt-4'
                  : ''}"
              >
                Carbs
              </div>
              <div
                class="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-sm"
              >
                <span>Total Carbs</span>
                <span class="font-medium tabular-nums">
                  {selectedDay.totalCarbs.toFixed(0)}g
                </span>
              </div>
            {/if}
          </div>
          <Separator class="mb-4" />
        {/if}

        <!-- Total Count -->
        <div class="mb-4">
          <div
            class="text-xs font-medium uppercase tracking-wide text-muted-foreground"
          >
            Total Records
          </div>
          <div class="mt-1 text-xl font-bold tabular-nums">
            {selectedDay.totalCount}
          </div>
        </div>

        <!-- Per-data-type Counts -->
        {#if getVisibleCounts(selectedDay.counts).length > 0}
          {@const visiblePanelCounts = getVisibleCounts(selectedDay.counts)}
          <Separator class="mb-4" />
          <div class="mb-4">
            <div
              class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground"
            >
              By Data Type
            </div>
            <div class="space-y-2">
              {#each visiblePanelCounts as [key, count]}
                <div
                  class="flex items-center justify-between rounded-md bg-muted/50 px-3 py-2 text-sm"
                >
                  <span>{getDataTypeLabel(key)}</span>
                  <span class="font-medium tabular-nums">{count}</span>
                </div>
              {/each}
            </div>
          </div>
        {/if}

        <!-- View Day in Review Button -->
        <div class="mt-6">
          <Button
            class="w-full gap-2"
            onclick={() => {
              if (selectedDay) navigateToDayInReview(selectedDay.dateString);
            }}
          >
            View Day in Review
            <ArrowRight class="h-4 w-4" />
          </Button>
        </div>
      </div>
    </div>
  {/if}
</div>

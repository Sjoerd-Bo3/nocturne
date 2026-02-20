<script lang="ts">
  import { goto } from "$app/navigation";
  import { browser } from "$app/environment";
  import { Chart, Svg, Calendar, Tooltip } from "layerchart";
  import { scaleThreshold } from "d3-scale";
  import {
    CalendarDays,
    X,
    ArrowRight,
    Loader2,
    Filter,
  } from "lucide-svelte";
  import * as Select from "$lib/components/ui/select";
  import { Button } from "$lib/components/ui/button";
  import { Separator } from "$lib/components/ui/separator";
  import {
    getAvailableYears,
    getDailySummary,
  } from "$api/generated/dataOverviews.generated.remote";
  import type { DailySummaryDay } from "$api/generated/nocturne-api-client";
  import { getDataTypeLabel } from "$lib/utils/data-type-labels";
  import {
    formatGlucoseValue,
    getUnitLabel,
  } from "$lib/utils/formatting";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import { getDateParamsContext } from "$lib/hooks/date-params.svelte";
  import { fly, fade, slide } from "svelte/transition";
  import { cubicOut } from "svelte/easing";

  // This report does NOT use the shared date params from layout for its primary data.
  // It manages its own year-based lazy loading. But we get a reference to navigate
  // to day-in-review with a specific date if the context exists.
  const reportsParams = getDateParamsContext();

  // =========================================================================
  // State
  // =========================================================================

  /** List of available years from the API */
  let availableYears = $state<number[]>([]);
  /** Available data sources from the API */
  let availableDataSources = $state<string[]>([]);
  /** Currently selected data source filter */
  let selectedDataSource = $state<string>("");
  /** Tracks the previous data source to detect changes */
  let prevDataSource = $state<string>("");
  /** Map of year -> loaded daily summary data */
  let yearData = $state<Map<number, DailySummaryDay[]>>(new Map());
  /** Set of years currently being loaded */
  let loadingYears = $state<Set<number>>(new Set());
  /** Whether the initial metadata has loaded */
  let metadataLoaded = $state(false);
  /** Whether initial metadata is loading */
  let metadataLoading = $state(false);
  /** The currently selected day for the detail panel */
  let selectedDay = $state<{
    date: string;
    averageGlucoseMgdl: number | null | undefined;
    totalCount: number;
    counts: Record<string, number>;
  } | null>(null);
  /** Sentinel element refs keyed by year */
  let sentinelElements: Record<number, HTMLDivElement | undefined> = $state({});

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

  const MUTED_COLOR = "hsl(var(--muted))";

  // =========================================================================
  // Derived
  // =========================================================================

  const units = $derived(glucoseUnits.current);
  const unitLabel = $derived(getUnitLabel(units));

  /** Years sorted in descending order (most recent first) */
  const sortedYears = $derived(
    [...availableYears].sort((a, b) => b - a)
  );

  // =========================================================================
  // Data Loading
  // =========================================================================

  /** Load the available years and data sources */
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

  /** Wait for a SvelteKit query to resolve */
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

  /** Load daily summary data for a specific year */
  async function loadYearData(year: number) {
    if (loadingYears.has(year) || yearData.has(year)) return;

    loadingYears = new Set([...loadingYears, year]);
    try {
      const params = selectedDataSource
        ? { year, dataSource: selectedDataSource }
        : { year };
      const result = getDailySummary(params);
      await waitForQuery(result);
      const days = result.current?.days ?? [];
      yearData = new Map([...yearData, [year, days]]);
    } catch (err) {
      console.error(`Failed to load data for year ${year}:`, err);
    } finally {
      const next = new Set(loadingYears);
      next.delete(year);
      loadingYears = next;
    }
  }

  /** Clear all loaded data and reload the first year */
  function clearAndReload() {
    yearData = new Map();
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
    averageGlucoseMgdl: number | null;
    counts: Record<string, number>;
    dateString: string;
  };

  /** Transform DailySummaryDay[] into data suitable for the Calendar chart */
  function transformYearData(days: DailySummaryDay[]): CalendarDatum[] {
    return days.map((day) => {
      const dateStr = day.date ?? "";
      const [y, m, d] = dateStr.split("-").map(Number);
      const date = new Date(Date.UTC(y, m - 1, d));
      const avg = day.averageGlucoseMgdl ?? null;

      return {
        date,
        value: avg,
        totalCount: day.totalCount ?? 0,
        averageGlucoseMgdl: avg,
        counts: (day.counts as Record<string, number>) ?? {},
        dateString: dateStr,
      };
    });
  }

  /** Get fill color for a calendar datum */
  function getCellColor(datum: CalendarDatum | undefined): string {
    if (!datum) return "transparent";
    if (datum.value != null) {
      return glucoseColorScale(datum.value);
    }
    if (datum.totalCount > 0) {
      return MUTED_COLOR;
    }
    return "transparent";
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
            const year = Number(
              (entry.target as HTMLElement).dataset.year
            );
            if (!isNaN(year)) {
              loadYearData(year);
            }
          }
        }
      },
      { rootMargin: "200px" }
    );

    // Observe all sentinel elements
    for (const year of sortedYears) {
      const el = sentinelElements[year];
      if (el) observer.observe(el);
    }
  }

  // =========================================================================
  // Day detail panel
  // =========================================================================

  function selectDay(datum: CalendarDatum) {
    if (datum.totalCount === 0) return;
    selectedDay = {
      date: datum.dateString,
      averageGlucoseMgdl: datum.averageGlucoseMgdl,
      totalCount: datum.totalCount,
      counts: datum.counts,
    };
  }

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

  // Load the first year once metadata arrives
  $effect(() => {
    if (metadataLoaded && sortedYears.length > 0) {
      loadYearData(sortedYears[0]);
    }
  });

  // Setup observer when sentinel elements appear
  $effect(() => {
    // Access sentinelElements to create reactive dependency (bind:this triggers updates)
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
    if (selectedDataSource !== prevDataSource && metadataLoaded) {
      prevDataSource = selectedDataSource;
      clearAndReload();
    }
  });

  // =========================================================================
  // Helpers
  // =========================================================================

  function getYearBounds(year: number): { start: Date; end: Date } {
    return {
      start: new Date(Date.UTC(year, 0, 1)),
      end: new Date(Date.UTC(year, 11, 31)),
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
</script>

<svelte:head>
  <title>Data Overview - Nocturne</title>
  <meta
    name="description"
    content="Multi-year heatmap overview of all your diabetes data"
  />
</svelte:head>

<div class="flex min-h-full">
  <!-- Main Content -->
  <div class="flex-1 transition-[margin] duration-200 {selectedDay ? 'mr-80 lg:mr-96' : ''}">
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
          <h1 class="text-2xl font-bold tracking-tight">Data Overview</h1>
          <p class="text-sm text-muted-foreground">
            Multi-year heatmap of all your data
          </p>
        </div>
      </div>

      <!-- Data Source Filter -->
      {#if availableDataSources.length > 0}
        <div class="flex items-center gap-2">
          <Filter class="h-4 w-4 text-muted-foreground" />
          <Select.Root
            type="single"
            value={selectedDataSource}
            onValueChange={(v) => {
              selectedDataSource = v === "__all__" ? "" : (v ?? "");
            }}
          >
            <Select.Trigger class="w-[200px]">
              <span class="truncate">
                {selectedDataSource
                  ? getDataTypeLabel(selectedDataSource)
                  : "All Data Sources"}
              </span>
            </Select.Trigger>
            <Select.Content>
              <Select.Item value="__all__">All Data Sources</Select.Item>
              {#each availableDataSources as source}
                <Select.Item value={source}>
                  {getDataTypeLabel(source)}
                </Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
        </div>
      {/if}
    </div>

    <!-- Color Legend -->
    <div
      class="mb-6 flex flex-wrap items-center gap-4 rounded-lg border border-border bg-card p-3 text-xs text-muted-foreground"
    >
      <span class="font-medium">Avg Glucose:</span>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: var(--glucose-very-low)"
        ></span>
        Very Low
      </div>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: var(--glucose-low)"
        ></span>
        Low
      </div>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: var(--glucose-in-range)"
        ></span>
        In Range
      </div>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: var(--glucose-high)"
        ></span>
        High
      </div>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: var(--glucose-very-high)"
        ></span>
        Very High
      </div>
      <div class="flex items-center gap-1.5">
        <span
          class="inline-block h-3 w-3 rounded-sm"
          style="background: hsl(var(--muted))"
        ></span>
        Other Data (no glucose)
      </div>
    </div>

    <!-- Loading state for metadata -->
    {#if metadataLoading && !metadataLoaded}
      <div
        class="flex items-center justify-center py-20"
        in:fade={{ duration: 200 }}
      >
        <div class="flex flex-col items-center gap-3">
          <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
          <p class="text-sm text-muted-foreground">
            Loading data overview...
          </p>
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
                <Loader2
                  class="h-4 w-4 animate-spin text-muted-foreground"
                />
              {/if}
              {#if days}
                <span class="text-sm text-muted-foreground">
                  {days.filter((d) => (d.totalCount ?? 0) > 0).length} days with
                  data
                </span>
              {/if}
            </div>

            <!-- Calendar Heatmap -->
            {#if chartData.length > 0}
              <div
                class="w-full overflow-x-auto rounded-lg border border-border bg-card p-4"
              >
                <div class="min-w-[750px]">
                  <Chart
                    data={chartData}
                    x="date"
                    tooltip={{ mode: "manual" }}
                  >
                    {#snippet children({ context })}
                      <Svg>
                        <Calendar
                          start={bounds.start}
                          end={bounds.end}
                          cellSize={14}
                          monthPath
                          monthLabel
                          tooltipContext={context.tooltip}
                        >
                          {#snippet children({ cells, cellSize })}
                            {#each cells as cell}
                              {@const datum = cell.data as CalendarDatum}
                              {@const fill = getCellColor(
                                datum?.dateString ? datum : undefined
                              )}
                              <rect
                                x={cell.x}
                                y={cell.y}
                                width={cellSize[0] - 1}
                                height={cellSize[1] - 1}
                                rx="2"
                                {fill}
                                role="button"
                                tabindex="-1"
                                class="cursor-pointer transition-opacity hover:opacity-80"
                                onpointermove={(e) =>
                                  context.tooltip?.show(e, cell.data)}
                                onpointerleave={() =>
                                  context.tooltip?.hide()}
                                onclick={() => {
                                  if (datum?.dateString) selectDay(datum);
                                }}
                                onkeydown={(e) => {
                                  if (e.key === "Enter" || e.key === " ") {
                                    e.preventDefault();
                                    if (datum?.dateString) selectDay(datum);
                                  }
                                }}
                              />
                            {/each}
                          {/snippet}
                        </Calendar>
                      </Svg>

                      <Tooltip.Root
                        class="rounded-md border bg-popover p-2 text-popover-foreground shadow-md"
                      >
                        {#snippet children({ data })}
                          {@const d = data as CalendarDatum}
                          {#if d?.dateString}
                            <div class="text-xs">
                              <div class="mb-1 font-medium">
                                {new Date(
                                  d.date.getUTCFullYear(),
                                  d.date.getUTCMonth(),
                                  d.date.getUTCDate()
                                ).toLocaleDateString(undefined, {
                                  weekday: "short",
                                  month: "short",
                                  day: "numeric",
                                })}
                              </div>
                              {#if d.averageGlucoseMgdl != null}
                                <div>
                                  Avg: {formatGlucoseValue(
                                    d.averageGlucoseMgdl,
                                    units
                                  )}
                                  {unitLabel}
                                </div>
                              {/if}
                              <div>
                                {d.totalCount}
                                {d.totalCount === 1 ? "record" : "records"}
                              </div>
                            </div>
                          {/if}
                        {/snippet}
                      </Tooltip.Root>
                    {/snippet}
                  </Chart>
                </div>
              </div>
            {:else if isYearLoading}
              <!-- Loading skeleton for year -->
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
              <!-- Empty year placeholder -->
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
            {formatSelectedDate(selectedDay.date)}
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
              {formatGlucoseValue(selectedDay.averageGlucoseMgdl, units)}
              <span class="text-sm font-normal text-muted-foreground">
                {unitLabel}
              </span>
            </div>
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
        {#if Object.keys(selectedDay.counts).length > 0}
          <Separator class="mb-4" />
          <div class="mb-4">
            <div
              class="mb-2 text-xs font-medium uppercase tracking-wide text-muted-foreground"
            >
              By Data Type
            </div>
            <div class="space-y-2">
              {#each Object.entries(selectedDay.counts).sort( ([, a], [, b]) => b - a ) as [key, count]}
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
              if (selectedDay) navigateToDayInReview(selectedDay.date);
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

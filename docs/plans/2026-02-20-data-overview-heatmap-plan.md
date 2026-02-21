# Data Overview Heatmap Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a multi-year GitHub-contributions-style heatmap report showing per-day record counts across all data types, colored by average glucose, with data-source filtering, lazy loading, and click-through navigation.

**Architecture:** New V4 endpoint aggregates day-level counts per SyncDataType + average glucose via EF Core GROUP BY queries across all data tables. Frontend uses LayerChart's `<Calendar>` component in a scrollable multi-year grid with IntersectionObserver-based lazy loading per year. Detail panel shows per-type breakdown on cell click.

**Tech Stack:** C# .NET 10 (controller + service + DTOs), EF Core (GROUP BY queries), SvelteKit 2 / Svelte 5 (page + components), LayerChart (`Calendar`, `Chart`, `Group`, `Text`, `Tooltip`), d3-scale (`scaleThreshold`), Zod 4 (input validation), shadcn-svelte (UI primitives).

**Design doc:** `docs/plans/2026-02-20-data-overview-heatmap-design.md`

---

## Task 1: DTOs

Create the response models used by the service and controller.

**Files:**
- Create: `src/Core/Nocturne.Core.Models/Services/DataOverviewModels.cs`

**Step 1: Create DTO file**

```csharp
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Services;

/// <summary>
/// Response for GET /api/v4/data-overview/years
/// </summary>
public class DataOverviewYearsResponse
{
    [JsonPropertyName("years")]
    public int[] Years { get; set; } = [];

    [JsonPropertyName("availableDataSources")]
    public string[] AvailableDataSources { get; set; } = [];
}

/// <summary>
/// Response for GET /api/v4/data-overview/daily-summary
/// </summary>
public class DailySummaryResponse
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("dataSource")]
    public string? DataSource { get; set; }

    [JsonPropertyName("days")]
    public DailySummaryDay[] Days { get; set; } = [];
}

/// <summary>
/// Aggregated data for a single day
/// </summary>
public class DailySummaryDay
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("averageGlucoseMgdl")]
    public double? AverageGlucoseMgdl { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Record counts keyed by SyncDataType name (e.g., "Glucose", "Boluses", "StateSpans")
    /// </summary>
    [JsonPropertyName("counts")]
    public Dictionary<string, int> Counts { get; set; } = new();
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/Core/Nocturne.Core.Models/Nocturne.Core.Models.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Core/Nocturne.Core.Models/Services/DataOverviewModels.cs
git commit -m "feat: add DataOverview response DTOs"
```

---

## Task 2: Service Interface

**Files:**
- Create: `src/Core/Nocturne.Core.Contracts/IDataOverviewService.cs`

**Step 1: Create interface**

```csharp
using Nocturne.Core.Models.Services;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for aggregating data overview statistics across all data types
/// </summary>
public interface IDataOverviewService
{
    /// <summary>
    /// Get the list of years that contain data and available data sources
    /// </summary>
    Task<DataOverviewYearsResponse> GetAvailableYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get day-level aggregated counts and average glucose for a given year
    /// </summary>
    /// <param name="year">The year to aggregate</param>
    /// <param name="dataSource">Optional data source filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<DailySummaryResponse> GetDailySummaryAsync(int year, string? dataSource = null, CancellationToken cancellationToken = default);
}
```

**Step 2: Verify it compiles**

Run: `dotnet build src/Core/Nocturne.Core.Contracts/Nocturne.Core.Contracts.csproj`
Expected: Build succeeded

**Step 3: Commit**

```bash
git add src/Core/Nocturne.Core.Contracts/IDataOverviewService.cs
git commit -m "feat: add IDataOverviewService interface"
```

---

## Task 3: Service Implementation

This is the core task. The service queries all data tables and merges results into day-level aggregates.

**Files:**
- Create: `src/API/Nocturne.API/Services/DataOverviewService.cs`

**Step 1: Create the service**

Key considerations:
- **V4 tables** (SensorGlucose, MeterGlucose, Boluses, CarbIntakes, BolusCalculations, Notes, DeviceEvents): all use `Mills` and `DataSource` columns
- **StateSpans**: uses `StartMills` and `Source` (NOT `Mills`/`DataSource`)
- **Activities**: has `Mills` but NO `DataSource` column — skip data source filter for this table
- **Foods**: has NO `Mills` and NO `DataSource` — use `SysCreatedAt` for date grouping, skip data source filter
- **DeviceStatuses**: legacy table with `Mills` but `Device` (no `DataSource`) — skip data source filter
- **Profiles**: legacy table — use `SysCreatedAt` for date grouping

The milliseconds-to-date conversion: compute `yearStartMills` and `yearEndMills` from the year parameter, then group by `DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime)` in EF — but since this is complex for EF to translate, use a helper approach: query the raw (date, count) pairs per table.

```csharp
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Services;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services;

public class DataOverviewService : IDataOverviewService
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<DataOverviewService> _logger;

    public DataOverviewService(NocturneDbContext context, ILogger<DataOverviewService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<DataOverviewYearsResponse> GetAvailableYearsAsync(CancellationToken cancellationToken = default)
    {
        // Collect years from all tables that have mills-based timestamps
        var yearQueries = new List<IQueryable<long>>
        {
            _context.SensorGlucose.Select(x => x.Mills),
            _context.MeterGlucose.Select(x => x.Mills),
            _context.Boluses.Select(x => x.Mills),
            _context.CarbIntakes.Select(x => x.Mills),
            _context.BolusCalculations.Select(x => x.Mills),
            _context.Notes.Select(x => x.Mills),
            _context.DeviceEvents.Select(x => x.Mills),
            _context.StateSpans.Select(x => x.StartMills),
            _context.Entries.Select(x => x.Mills),
            _context.Treatments.Select(x => x.Mills),
            _context.Activities.Select(x => x.Mills),
        };

        var years = new HashSet<int>();
        foreach (var q in yearQueries)
        {
            var minMax = await q
                .GroupBy(_ => 1)
                .Select(g => new { Min = g.Min(), Max = g.Max() })
                .FirstOrDefaultAsync(cancellationToken);

            if (minMax is { Min: > 0, Max: > 0 })
            {
                var minYear = DateTimeOffset.FromUnixTimeMilliseconds(minMax.Min).UtcDateTime.Year;
                var maxYear = DateTimeOffset.FromUnixTimeMilliseconds(minMax.Max).UtcDateTime.Year;
                for (var y = minYear; y <= maxYear; y++)
                    years.Add(y);
            }
        }

        // Collect distinct data sources from tables that have the DataSource column
        var sources = new HashSet<string>();

        var sgSources = await _context.SensorGlucose
            .Where(x => x.DataSource != null)
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(sgSources);

        var mgSources = await _context.MeterGlucose
            .Where(x => x.DataSource != null)
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(mgSources);

        var bolusSources = await _context.Boluses
            .Where(x => x.DataSource != null)
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(bolusSources);

        var carbSources = await _context.CarbIntakes
            .Where(x => x.DataSource != null)
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(carbSources);

        var entrySources = await _context.Entries
            .Where(x => x.DataSource != null && x.DataSource != "")
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(entrySources);

        var treatmentSources = await _context.Treatments
            .Where(x => x.DataSource != null && x.DataSource != "")
            .Select(x => x.DataSource!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(treatmentSources);

        var stateSpanSources = await _context.StateSpans
            .Where(x => x.Source != null && x.Source != "")
            .Select(x => x.Source!)
            .Distinct()
            .ToListAsync(cancellationToken);
        sources.UnionWith(stateSpanSources);

        return new DataOverviewYearsResponse
        {
            Years = years.OrderDescending().ToArray(),
            AvailableDataSources = sources.Order().ToArray()
        };
    }

    public async Task<DailySummaryResponse> GetDailySummaryAsync(
        int year, string? dataSource = null, CancellationToken cancellationToken = default)
    {
        var yearStart = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearEnd = new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startMills = yearStart.ToUnixTimeMilliseconds();
        var endMills = yearEnd.ToUnixTimeMilliseconds();

        // Dictionary keyed by date string "YYYY-MM-DD" → DailySummaryDay
        var dayMap = new Dictionary<string, DailySummaryDay>();

        // Helper to ensure a day entry exists
        DailySummaryDay GetOrCreateDay(string dateStr)
        {
            if (!dayMap.TryGetValue(dateStr, out var day))
            {
                day = new DailySummaryDay { Date = dateStr };
                dayMap[dateStr] = day;
            }
            return day;
        }

        // Helper to convert mills to date string
        static string MillsToDateStr(long mills) =>
            DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime.ToString("yyyy-MM-dd");

        // --- V4 tables with Mills + DataSource ---
        await AggregateTable(_context.SensorGlucose
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "Glucose", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.MeterGlucose
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "ManualBG", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.Boluses
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "Boluses", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.CarbIntakes
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "CarbIntake", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.BolusCalculations
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "BolusCalculations", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.Notes
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "Notes", GetOrCreateDay, cancellationToken);

        await AggregateTable(_context.DeviceEvents
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource),
            x => x.Mills, "DeviceEvents", GetOrCreateDay, cancellationToken);

        // --- StateSpans: uses StartMills + Source ---
        await AggregateTable(_context.StateSpans
                .Where(x => x.StartMills >= startMills && x.StartMills < endMills)
                .Where(x => dataSource == null || x.Source == dataSource),
            x => x.StartMills, "StateSpans", GetOrCreateDay, cancellationToken);

        // --- Legacy Entries (sgv type for glucose fallback) ---
        await AggregateTable(_context.Entries
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource)
                .Where(x => x.Type == "sgv"),
            x => x.Mills, "Glucose", GetOrCreateDay, cancellationToken);

        // --- Legacy Entries (mbg type for manual BG) ---
        await AggregateTable(_context.Entries
                .Where(x => x.Mills >= startMills && x.Mills < endMills)
                .Where(x => dataSource == null || x.DataSource == dataSource)
                .Where(x => x.Type == "mbg"),
            x => x.Mills, "ManualBG", GetOrCreateDay, cancellationToken);

        // --- Activities: has Mills but NO DataSource column ---
        if (dataSource == null)
        {
            await AggregateTable(_context.Activities
                    .Where(x => x.Mills >= startMills && x.Mills < endMills),
                x => x.Mills, "Activity", GetOrCreateDay, cancellationToken);
        }

        // --- DeviceStatuses: has Mills but no DataSource ---
        if (dataSource == null)
        {
            await AggregateTable(_context.DeviceStatuses
                    .Where(x => x.Mills >= startMills && x.Mills < endMills),
                x => x.Mills, "DeviceStatus", GetOrCreateDay, cancellationToken);
        }

        // --- Glucose averages (from sensor_glucose + legacy entries) ---
        var glucoseAvgs = await _context.SensorGlucose
            .Where(x => x.Mills >= startMills && x.Mills < endMills)
            .Where(x => dataSource == null || x.DataSource == dataSource)
            .Where(x => x.Mgdl > 0)
            .GroupBy(x => EF.Functions.DateTruncate("day",
                DateTimeOffset.FromUnixTimeMilliseconds(x.Mills).UtcDateTime))
            .Select(g => new { Day = g.Key, Avg = g.Average(x => x.Mgdl) })
            .ToListAsync(cancellationToken);

        foreach (var ga in glucoseAvgs)
        {
            if (ga.Day is not null)
            {
                var dateStr = DateOnly.FromDateTime((DateTime)ga.Day).ToString("yyyy-MM-dd");
                var day = GetOrCreateDay(dateStr);
                day.AverageGlucoseMgdl = Math.Round(ga.Avg, 1);
            }
        }

        // Also add legacy entry glucose averages if no V4 data for that day
        var legacyGlucoseAvgs = await _context.Entries
            .Where(x => x.Mills >= startMills && x.Mills < endMills)
            .Where(x => dataSource == null || x.DataSource == dataSource)
            .Where(x => x.Type == "sgv" && x.Mgdl > 0)
            .GroupBy(x => EF.Functions.DateTruncate("day",
                DateTimeOffset.FromUnixTimeMilliseconds(x.Mills).UtcDateTime))
            .Select(g => new { Day = g.Key, Avg = g.Average(x => x.Mgdl) })
            .ToListAsync(cancellationToken);

        foreach (var ga in legacyGlucoseAvgs)
        {
            if (ga.Day is not null)
            {
                var dateStr = DateOnly.FromDateTime((DateTime)ga.Day).ToString("yyyy-MM-dd");
                var day = GetOrCreateDay(dateStr);
                // Only set if not already set by V4 data
                day.AverageGlucoseMgdl ??= Math.Round(ga.Avg, 1);
            }
        }

        // Compute totals
        foreach (var day in dayMap.Values)
        {
            day.TotalCount = day.Counts.Values.Sum();
        }

        return new DailySummaryResponse
        {
            Year = year,
            DataSource = dataSource,
            Days = dayMap.Values.OrderBy(d => d.Date).ToArray()
        };
    }

    /// <summary>
    /// Generic helper: groups a queryable by day (from Mills) and adds counts to the day map
    /// </summary>
    private async Task AggregateTable<T>(
        IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, long>> millsSelector,
        string dataType,
        Func<string, DailySummaryDay> getOrCreateDay,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            // We need to materialize mills and count in-memory because EF can't translate
            // DateTimeOffset.FromUnixTimeMilliseconds in GROUP BY for all providers.
            // Instead, project just the mills values grouped by a truncated expression.
            // Use raw SQL approach: extract date from mills via PostgreSQL function.
            var counts = await query
                .GroupBy(millsSelector)
                .Select(g => new { Mills = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            // Group by date in memory (fast — just grouping ints)
            var byDate = counts
                .GroupBy(x => DateTimeOffset.FromUnixTimeMilliseconds(x.Mills).UtcDateTime.Date)
                .Select(g => new { Date = g.Key, Count = g.Sum(x => x.Count) });

            foreach (var item in byDate)
            {
                var dateStr = item.Date.ToString("yyyy-MM-dd");
                var day = getOrCreateDay(dateStr);
                day.Counts.TryGetValue(dataType, out var existing);
                day.Counts[dataType] = existing + item.Count;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to aggregate {DataType} table", dataType);
        }
    }
}
```

**Important note about the AggregateTable helper:** The initial approach groups by the raw mills value (which produces many groups — one per unique mills). For tables with many records, this is inefficient. A better approach is to use raw SQL or a PostgreSQL-specific date truncation. However, for correctness-first, we start with this approach and can optimize later. The glucose average queries use `EF.Functions.DateTruncate` which PostgreSQL Npgsql supports.

**Alternative (if EF translation fails):** Fall back to `FromSqlRaw`:
```csharp
var results = await _context.Database
    .SqlQueryRaw<DateCount>(
        "SELECT DATE(to_timestamp(mills / 1000.0)) AS day, COUNT(*)::int AS count FROM sensor_glucose WHERE mills >= {0} AND mills < {1} GROUP BY day",
        startMills, endMills)
    .ToListAsync(cancellationToken);
```

**Step 2: Verify it compiles**

Run: `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=false`
Expected: Build succeeded (use `-p:GenerateNSwagClient=false` to avoid NSwag needing a running server)

**Step 3: Commit**

```bash
git add src/API/Nocturne.API/Services/DataOverviewService.cs
git commit -m "feat: implement DataOverviewService with per-table aggregation"
```

---

## Task 4: Controller

**Files:**
- Create: `src/API/Nocturne.API/Controllers/V4/DataOverviewController.cs`

**Step 1: Create controller**

```csharp
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Services;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Data overview controller for aggregated statistics across all data types
/// </summary>
[ApiController]
[Route("api/v4/data-overview")]
[Tags("V4 Data Overview")]
[ClientPropertyName("dataOverview")]
public class DataOverviewController : ControllerBase
{
    private readonly IDataOverviewService _dataOverviewService;
    private readonly ILogger<DataOverviewController> _logger;

    public DataOverviewController(IDataOverviewService dataOverviewService, ILogger<DataOverviewController> logger)
    {
        _dataOverviewService = dataOverviewService;
        _logger = logger;
    }

    /// <summary>
    /// Get the list of years that contain data and available data sources
    /// </summary>
    [HttpGet("years")]
    [RemoteQuery]
    [ProducesResponseType(typeof(DataOverviewYearsResponse), 200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DataOverviewYearsResponse>> GetAvailableYears(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dataOverviewService.GetAvailableYearsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available years for data overview");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get day-level aggregated counts and average glucose for a given year
    /// </summary>
    /// <param name="year">The year to aggregate</param>
    /// <param name="dataSource">Optional data source filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("daily-summary")]
    [RemoteQuery]
    [ProducesResponseType(typeof(DailySummaryResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<DailySummaryResponse>> GetDailySummary(
        [FromQuery] int year,
        [FromQuery] string? dataSource = null,
        CancellationToken cancellationToken = default)
    {
        if (year < 2000 || year > 2100)
            return BadRequest(new { error = "Year must be between 2000 and 2100" });

        try
        {
            var result = await _dataOverviewService.GetDailySummaryAsync(year, dataSource, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily summary for year {Year}", year);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
```

**Step 2: Register the service in Program.cs**

In `src/API/Nocturne.API/Program.cs`, add alongside the other `AddScoped` registrations (around line ~440):

```csharp
builder.Services.AddScoped<IDataOverviewService, DataOverviewService>();
```

**Step 3: Verify it compiles**

Run: `dotnet build src/API/Nocturne.API/Nocturne.API.csproj -p:GenerateNSwagClient=false`
Expected: Build succeeded

**Step 4: Commit**

```bash
git add src/API/Nocturne.API/Controllers/V4/DataOverviewController.cs src/API/Nocturne.API/Program.cs
git commit -m "feat: add DataOverviewController with years and daily-summary endpoints"
```

---

## Task 5: Unit Tests

**Files:**
- Create: `tests/Unit/Nocturne.API.Tests/Services/DataOverviewServiceTests.cs`

**Step 1: Write tests**

```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Services;

[Trait("Category", "Unit")]
public class DataOverviewServiceTests
{
    private readonly Mock<ILogger<DataOverviewService>> _loggerMock = new();

    private (NocturneDbContext context, DataOverviewService service) CreateService()
    {
        var context = TestDbContextFactory.CreateInMemoryContext();
        var service = new DataOverviewService(context, _loggerMock.Object);
        return (context, service);
    }

    [Fact]
    public async Task GetAvailableYearsAsync_EmptyDatabase_ReturnsEmptyArrays()
    {
        var (context, service) = CreateService();
        using var _ = context;

        var result = await service.GetAvailableYearsAsync();

        result.Years.Should().BeEmpty();
        result.AvailableDataSources.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAvailableYearsAsync_WithSensorGlucose_ReturnsYears()
    {
        var (context, service) = CreateService();
        using var _ = context;

        // Jan 15, 2024 in mills
        var mills2024 = new DateTimeOffset(2024, 1, 15, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        // Jun 1, 2025 in mills
        var mills2025 = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        context.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(), Mills = mills2024, Mgdl = 120, DataSource = "dexcom-connector"
        });
        context.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(), Mills = mills2025, Mgdl = 130, DataSource = "dexcom-connector"
        });
        await context.SaveChangesAsync();

        var result = await service.GetAvailableYearsAsync();

        result.Years.Should().BeEquivalentTo([2025, 2024]);
        result.AvailableDataSources.Should().Contain("dexcom-connector");
    }

    [Fact]
    public async Task GetDailySummaryAsync_CountsPerDataType()
    {
        var (context, service) = CreateService();
        using var _ = context;

        // 3 glucose readings on Jan 10, 2025
        var jan10Mills = new DateTimeOffset(2025, 1, 10, 8, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();
        for (var i = 0; i < 3; i++)
        {
            context.SensorGlucose.Add(new SensorGlucoseEntity
            {
                Id = Guid.NewGuid(),
                Mills = jan10Mills + i * 300_000, // 5 min apart
                Mgdl = 100 + i * 20,
                DataSource = "test-source"
            });
        }

        // 1 bolus on Jan 10, 2025
        context.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = jan10Mills + 600_000,
            DataSource = "test-source"
        });

        await context.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(2025);

        result.Year.Should().Be(2025);
        result.Days.Should().HaveCount(1);

        var day = result.Days[0];
        day.Date.Should().Be("2025-01-10");
        day.Counts.Should().ContainKey("Glucose").WhoseValue.Should().Be(3);
        day.Counts.Should().ContainKey("Boluses").WhoseValue.Should().Be(1);
        day.TotalCount.Should().Be(4);
        day.AverageGlucoseMgdl.Should().BeApproximately(120.0, 0.5);
    }

    [Fact]
    public async Task GetDailySummaryAsync_FilterByDataSource_OnlyCountsMatchingSource()
    {
        var (context, service) = CreateService();
        using var _ = context;

        var mills = new DateTimeOffset(2025, 3, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        context.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(), Mills = mills, Mgdl = 150, DataSource = "dexcom-connector"
        });
        context.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(), Mills = mills + 300_000, Mgdl = 160, DataSource = "glooko-connector"
        });

        await context.SaveChangesAsync();

        var result = await service.GetDailySummaryAsync(2025, "dexcom-connector");

        result.DataSource.Should().Be("dexcom-connector");
        result.Days.Should().HaveCount(1);
        result.Days[0].Counts["Glucose"].Should().Be(1);
    }
}
```

**Step 2: Run tests**

Run: `dotnet test tests/Unit/Nocturne.API.Tests/ --filter "FullyQualifiedName~DataOverviewServiceTests" -v normal`
Expected: All 4 tests pass

**Note:** The in-memory provider may not support `EF.Functions.DateTruncate`. If the glucose average tests fail due to this, the service implementation will need a fallback path (materialize then group in-memory) which is acceptable for correctness. Fix any EF translation issues as they arise.

**Step 3: Commit**

```bash
git add tests/Unit/Nocturne.API.Tests/Services/DataOverviewServiceTests.cs
git commit -m "test: add DataOverviewService unit tests"
```

---

## Task 6: Regenerate NSwag Client

**Step 1: Start Aspire to regenerate the NSwag client**

Run: `aspire run`

Wait for the application to start. This triggers the NSwag client generation which will pick up the new `DataOverviewController` endpoints and generate TypeScript types.

**Step 2: Verify the generated client**

Check that `src/Web/packages/app/src/lib/api/generated/nocturne-api-client.ts` now contains:
- `DataOverviewYearsResponse` interface
- `DailySummaryResponse` interface
- `DailySummaryDay` interface
- `dataOverview_getAvailableYears()` method
- `dataOverview_getDailySummary(year, dataSource)` method

Also check that `src/Web/packages/app/src/lib/api/generated/openapi.json` contains the new endpoints.

**Step 3: Verify auto-generated remote functions**

Check for a new generated file like `src/Web/packages/app/src/lib/api/generated/data-overview.generated.remote.ts` (or similar naming) containing the `getAvailableYears` and `getDailySummary` query wrappers.

**Step 4: Commit generated files**

```bash
git add src/Web/packages/app/src/lib/api/generated/
git commit -m "chore: regenerate NSwag client with data-overview endpoints"
```

---

## Task 7: Frontend Page — Basic Route and Data Loading

**Files:**
- Create: `src/Web/packages/app/src/routes/reports/data-overview/+page.svelte`

**Step 1: Create the page with data loading skeleton**

Start with the basic structure: load years, load current year's data, render a placeholder. We'll add the LayerChart calendar in the next task.

```svelte
<script lang="ts">
  import { goto } from "$app/navigation";
  import { getAvailableYears, getDailySummary } from "$api/generated/data-overview.generated.remote";
  import { getDataTypeLabel } from "$lib/utils/data-type-labels";
  import { glucoseUnits } from "$lib/stores/appearance-store.svelte";
  import { formatGlucoseValue, getUnitLabel } from "$lib/utils/formatting";
  import { Button } from "$lib/components/ui/button";
  import * as Card from "$lib/components/ui/card";
  import * as Select from "$lib/components/ui/select";
  import { X, ExternalLink, CalendarDays } from "lucide-svelte";
  import { cn } from "$lib/utils";

  // Types inferred from API response
  type DailySummaryDay = NonNullable<
    Awaited<ReturnType<typeof getDailySummary>>
  >["days"][number];

  // State
  let selectedDataSource = $state<string | undefined>(undefined);
  let selectedDay = $state<DailySummaryDay | null>(null);
  let loadedYears = $state<Map<number, DailySummaryDay[]>>(new Map());
  let loadingYears = $state<Set<number>>(new Set());

  // Queries
  const yearsQuery = $derived(getAvailableYears());

  const units = $derived(glucoseUnits.current);
  const unitLabel = $derived(getUnitLabel(units));

  // Load a specific year's data
  async function loadYear(year: number) {
    if (loadedYears.has(year) || loadingYears.has(year)) return;
    loadingYears = new Set([...loadingYears, year]);

    try {
      const result = await getDailySummary({ year, dataSource: selectedDataSource });
      loadedYears = new Map([...loadedYears, [year, result?.days ?? []]]);
    } finally {
      const next = new Set(loadingYears);
      next.delete(year);
      loadingYears = next;
    }
  }

  // When data source changes, reload everything
  function handleDataSourceChange(value: string | undefined) {
    selectedDataSource = value === "all" ? undefined : value;
    loadedYears = new Map();
    loadingYears = new Set();
    selectedDay = null;
    // Reload current year
    const currentYear = new Date().getFullYear();
    loadYear(currentYear);
  }

  // Load current year on mount
  $effect(() => {
    const currentYear = new Date().getFullYear();
    loadYear(currentYear);
  });

  // Navigation helpers
  function goToCalendarMonth(year: number, month: number) {
    goto(`/calendar?year=${year}&month=${month}`);
  }

  function goToWeek(dateStr: string) {
    goto(`/reports/week-to-week?date=${dateStr}`);
  }

  function goToDayInReview(dateStr: string) {
    goto(`/reports/day-in-review?date=${dateStr}`);
  }
</script>

{#await yearsQuery}
  <div class="flex items-center justify-center h-64">
    <div class="text-muted-foreground">Loading data overview...</div>
  </div>
{:then yearsData}
  <div class="flex gap-6 h-full">
    <!-- Main heatmap area -->
    <div class="flex-1 min-w-0">
      <!-- Controls -->
      <div class="flex items-center justify-between mb-6">
        <div class="flex items-center gap-3">
          <CalendarDays class="h-5 w-5 text-muted-foreground" />
          <h2 class="text-lg font-semibold">Data Overview</h2>
        </div>

        {#if yearsData?.availableDataSources?.length}
          <Select.Root
            type="single"
            value={selectedDataSource ?? "all"}
            onValueChange={(v) => handleDataSourceChange(v)}
          >
            <Select.Trigger class="w-[200px]">
              {selectedDataSource ?? "All Data Sources"}
            </Select.Trigger>
            <Select.Content>
              <Select.Item value="all">All Data Sources</Select.Item>
              {#each yearsData.availableDataSources as source}
                <Select.Item value={source}>{source}</Select.Item>
              {/each}
            </Select.Content>
          </Select.Root>
        {/if}
      </div>

      <!-- Year sections (placeholder for LayerChart — Task 8) -->
      <div class="space-y-8 overflow-y-auto">
        {#each yearsData?.years ?? [] as year}
          <div class="year-section" data-year={year}>
            <h3 class="text-sm font-medium text-muted-foreground mb-2">{year}</h3>
            {#if loadedYears.has(year)}
              <Card.Root>
                <Card.Content class="p-4">
                  <div class="text-sm text-muted-foreground">
                    {loadedYears.get(year)?.length ?? 0} days with data
                    <!-- LayerChart Calendar will go here in Task 8 -->
                  </div>
                </Card.Content>
              </Card.Root>
            {:else if loadingYears.has(year)}
              <Card.Root>
                <Card.Content class="p-4">
                  <div class="h-[120px] animate-pulse bg-muted rounded" />
                </Card.Content>
              </Card.Root>
            {:else}
              <!-- Intersection observer target for lazy loading -->
              <div class="h-[140px]" />
            {/if}
          </div>
        {/each}
      </div>
    </div>

    <!-- Detail panel -->
    {#if selectedDay}
      <div class="w-80 shrink-0 border-l pl-6">
        <div class="sticky top-20">
          <div class="flex items-center justify-between mb-4">
            <h3 class="font-semibold">
              {new Date(selectedDay.date + "T00:00:00").toLocaleDateString(undefined, {
                weekday: "long",
                year: "numeric",
                month: "long",
                day: "numeric",
              })}
            </h3>
            <Button
              variant="ghost"
              size="icon"
              class="h-6 w-6"
              onclick={() => (selectedDay = null)}
            >
              <X class="h-4 w-4" />
            </Button>
          </div>

          {#if selectedDay.averageGlucoseMgdl}
            <div class="mb-4 p-3 rounded-lg bg-muted/50">
              <div class="text-xs text-muted-foreground">Average Glucose</div>
              <div class="text-2xl font-bold">
                {formatGlucoseValue(selectedDay.averageGlucoseMgdl, units)}
                <span class="text-sm font-normal text-muted-foreground">{unitLabel}</span>
              </div>
            </div>
          {/if}

          <div class="space-y-2 mb-6">
            <div class="text-xs font-medium text-muted-foreground uppercase tracking-wide">
              Record Counts
            </div>
            {#each Object.entries(selectedDay.counts ?? {}).filter(([, v]) => v > 0).sort(([, a], [, b]) => b - a) as [type, count]}
              <div class="flex items-center justify-between py-1.5 px-2 rounded hover:bg-muted/50">
                <span class="text-sm">{getDataTypeLabel(type)}</span>
                <span class="text-sm font-medium tabular-nums">{count.toLocaleString()}</span>
              </div>
            {/each}
          </div>

          <div class="text-xs text-muted-foreground mb-2">
            Total: {selectedDay.totalCount.toLocaleString()} records
          </div>

          <Button
            class="w-full gap-2"
            onclick={() => goToDayInReview(selectedDay!.date)}
          >
            <ExternalLink class="h-4 w-4" />
            View Day in Review
          </Button>
        </div>
      </div>
    {/if}
  </div>
{:catch error}
  <div class="flex items-center justify-center h-64">
    <div class="text-center">
      <p class="text-destructive font-medium">Failed to load data overview</p>
      <p class="text-sm text-muted-foreground mt-1">
        {error instanceof Error ? error.message : "An error occurred"}
      </p>
    </div>
  </div>
{/await}
```

**Step 2: Verify the page compiles**

Run: `cd src/Web && pnpm run check`
Expected: No type errors related to data-overview page

**Note:** The import path for the generated remote functions (`$api/generated/data-overview.generated.remote`) will depend on the exact filename the codegen produces. Check the actual generated filename and adjust the import.

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/reports/data-overview/+page.svelte
git commit -m "feat: add data-overview report page with data loading and detail panel"
```

---

## Task 8: LayerChart Calendar Heatmap

Replace the placeholder in the page with the actual LayerChart multi-year calendar.

**Files:**
- Create: `src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte`
- Modify: `src/Web/packages/app/src/routes/reports/data-overview/+page.svelte`

**Step 1: Create CalendarHeatmap component**

This component receives loaded year data and renders the LayerChart Calendar grid.

```svelte
<script lang="ts">
  import { Chart, Calendar, Group, Text, Svg, Rect, Tooltip } from "layerchart";
  import { scaleThreshold } from "d3-scale";
  import { format } from "date-fns";
  import { endOfYear } from "date-fns";

  type DayData = {
    date: Date;
    averageGlucoseMgdl: number | null;
    totalCount: number;
    counts: Record<string, number>;
    dateStr: string;
  };

  let {
    years,
    yearData,
    onCellClick,
    onMonthClick,
    onWeekClick,
  }: {
    years: number[];
    yearData: Map<number, DayData[]>;
    onCellClick: (dateStr: string) => void;
    onMonthClick: (year: number, month: number) => void;
    onWeekClick: (dateStr: string) => void;
  } = $props();

  const CELL_SIZE = 14;
  const YEAR_HEIGHT = CELL_SIZE * 7 + 30; // 7 rows + label space
  const YEAR_GAP = 20;

  // Flatten all year data into a single array for Chart
  const allData = $derived.by(() => {
    const result: DayData[] = [];
    for (const [, days] of yearData) {
      result.push(...days);
    }
    return result;
  });

  const totalHeight = $derived(years.length * (YEAR_HEIGHT + YEAR_GAP));

  // Glucose color scale
  const glucoseColorScale = scaleThreshold<number, string>()
    .domain([54, 70, 180, 250])
    .range([
      "hsl(var(--glucose-very-low))",
      "hsl(var(--glucose-low))",
      "hsl(var(--glucose-in-range))",
      "hsl(var(--glucose-high))",
      "hsl(var(--glucose-very-high))",
    ]);
</script>

<div style="height: {totalHeight}px" class="w-full">
  <Chart
    data={allData}
    x="date"
    c="averageGlucoseMgdl"
    cScale={glucoseColorScale}
    padding={{ left: 40, top: 10, right: 10, bottom: 10 }}
    let:tooltip
  >
    <Svg>
      {#each years as year, i}
        {@const start = new Date(year, 0, 1)}
        {@const end = endOfYear(start)}
        {@const yOffset = i * (YEAR_HEIGHT + YEAR_GAP)}
        {@const hasData = yearData.has(year)}
        <Group y={yOffset}>
          <!-- Year label (rotated) -->
          <Text
            value={year}
            class="text-xs font-medium fill-muted-foreground"
            rotate={270}
            x={-20}
            y={(CELL_SIZE * 7) / 2}
            textAnchor="middle"
            verticalAnchor="start"
          />

          {#if hasData}
            <Calendar
              {start}
              {end}
              {tooltip}
              cellSize={CELL_SIZE}
              monthPath={{ class: "stroke-border/30 fill-none" }}
            >
              {#snippet children({ cells })}
                {#each cells as cell}
                  <!-- svelte-ignore a11y_click_events_have_key_events -->
                  <!-- svelte-ignore a11y_no_static_element_interactions -->
                  <g
                    onclick={() => {
                      if (cell.data?.dateStr) onCellClick(cell.data.dateStr);
                    }}
                    class="cursor-pointer"
                  >
                    <Rect
                      x={cell.x}
                      y={cell.y}
                      width={CELL_SIZE}
                      height={CELL_SIZE}
                      fill={cell.color === "transparent" && cell.data?.totalCount > 0
                        ? "hsl(var(--muted))"
                        : cell.color || "hsl(var(--muted) / 0.3)"}
                      rx={2}
                      class="stroke-background hover:stroke-foreground/50"
                      onpointermove={(e) => tooltip?.show(e, cell.data)}
                      onpointerleave={() => tooltip?.hide()}
                    />
                  </g>
                {/each}
              {/snippet}
            </Calendar>
          {:else}
            <!-- Loading placeholder -->
            {#each Array(53) as _, week}
              {#each Array(7) as _, day}
                <Rect
                  x={week * CELL_SIZE}
                  y={day * CELL_SIZE}
                  width={CELL_SIZE}
                  height={CELL_SIZE}
                  fill="hsl(var(--muted) / 0.15)"
                  rx={2}
                  class="animate-pulse"
                />
              {/each}
            {/each}
          {/if}
        </Group>
      {/each}
    </Svg>

    <Tooltip.Root let:data>
      {#if data?.dateStr}
        <Tooltip.Header>
          {new Date(data.dateStr + "T00:00:00").toLocaleDateString(undefined, {
            weekday: "short",
            month: "short",
            day: "numeric",
            year: "numeric",
          })}
        </Tooltip.Header>
        {#if data.averageGlucoseMgdl != null}
          <Tooltip.List>
            <Tooltip.Item
              label="Avg Glucose"
              value="{data.averageGlucoseMgdl} mg/dL"
              valueAlign="right"
            />
            <Tooltip.Item
              label="Total Records"
              value={data.totalCount}
              format="integer"
              valueAlign="right"
            />
          </Tooltip.List>
        {:else}
          <Tooltip.List>
            <Tooltip.Item
              label="Records"
              value={data.totalCount}
              format="integer"
              valueAlign="right"
            />
          </Tooltip.List>
        {/if}
      {/if}
    </Tooltip.Root>
  </Chart>
</div>
```

**Step 2: Wire up the CalendarHeatmap in the page**

In the page component, replace the `<!-- LayerChart Calendar will go here in Task 8 -->` placeholder section with the CalendarHeatmap component. Transform the loaded data into the format the component expects (converting date strings to Date objects).

**Step 3: Add IntersectionObserver for lazy loading**

Use a Svelte `$effect` with `IntersectionObserver` to watch `.year-section` elements and trigger `loadYear()` when they enter the viewport.

```typescript
// In the page <script>
$effect(() => {
  const observer = new IntersectionObserver(
    (entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          const year = Number(entry.target.getAttribute("data-year"));
          if (year && !loadedYears.has(year) && !loadingYears.has(year)) {
            loadYear(year);
          }
        }
      }
    },
    { rootMargin: "200px" }
  );

  const sections = document.querySelectorAll(".year-section");
  sections.forEach((el) => observer.observe(el));

  return () => observer.disconnect();
});
```

**Step 4: Verify it renders**

Run `aspire run`, navigate to `/reports/data-overview`, and verify:
- Years list loads
- Current year's heatmap renders with colored cells
- Scrolling down triggers loading of older years
- Hovering cells shows tooltip
- Clicking a cell opens the detail panel

**Step 5: Commit**

```bash
git add src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte
git add src/Web/packages/app/src/routes/reports/data-overview/+page.svelte
git commit -m "feat: add LayerChart calendar heatmap with lazy loading and tooltips"
```

---

## Task 9: Navigation Links (Month, Week, Detail Panel)

**Files:**
- Modify: `src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte`

**Step 1: Add clickable month labels**

The LayerChart Calendar renders month labels via `monthPath`. To make month labels clickable, add `<Text>` elements for each month with click handlers positioned above the calendar cells:

```svelte
<!-- Inside the <Group> for each year, after <Calendar> -->
{#each Array(12) as _, monthIdx}
  {@const monthStart = new Date(year, monthIdx, 1)}
  <!-- Calculate x position based on week offset -->
  <text
    x={/* week offset calculation */}
    y={-8}
    class="text-[10px] fill-muted-foreground hover:fill-primary cursor-pointer"
    onclick={() => onMonthClick(year, monthIdx + 1)}
  >
    {format(monthStart, "MMM")}
  </text>
{/each}
```

**Step 2: Add week click zones**

Add invisible click targets for each week row (7 cells wide) that trigger navigation to the week-to-week report. These should be subtle — only activate on a narrow strip at the left edge, so they don't conflict with individual cell clicks.

**Step 3: Verify navigation**

- Click a month label → navigates to `/calendar?year=YYYY&month=MM`
- Click a week edge → navigates to `/reports/week-to-week?date=YYYY-MM-DD`
- Click a cell → opens detail panel
- Detail panel "View Day in Review" → navigates to `/reports/day-in-review?date=YYYY-MM-DD`

**Step 4: Commit**

```bash
git add src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte
git commit -m "feat: add month and week navigation links to calendar heatmap"
```

---

## Task 10: Reports Index Integration

**Files:**
- Modify: `src/Web/packages/app/src/routes/reports/+page.svelte`

**Step 1: Add the Data Overview entry**

Add to the `reportCategories` array, in the "patterns" category (second category, `id: "patterns"`):

```typescript
{
  title: "Data Overview",
  description: "Multi-year heatmap of all your data",
  href: "/reports/data-overview",
  icon: CalendarDays,
  status: "available" as const,
},
```

Also add `CalendarDays` to the lucide-svelte import at the top of the file.

**Step 2: Verify it appears**

Navigate to `/reports` and confirm "Data Overview" appears in the "Patterns & Trends" category.

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/reports/+page.svelte
git commit -m "feat: add Data Overview to reports index page"
```

---

## Task 11: Polish and Edge Cases

**Files:**
- Modify: `src/Web/packages/app/src/routes/reports/data-overview/+page.svelte`
- Modify: `src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte`

**Step 1: Handle empty state**

When there's no data at all (no years returned), show a friendly empty state.

**Step 2: Handle data source filter resetting the view**

When the user changes the data source filter, scroll back to the top and reload data.

**Step 3: Add color legend**

Below the controls, add a small legend showing the glucose color scale:
```
Very Low | Low | In Range | High | Very High | No Glucose Data
```

**Step 4: Responsive layout**

On narrow screens (< 768px), the detail panel should appear as a bottom sheet or modal instead of a side panel.

**Step 5: Final visual check**

Run `aspire run` and test the complete flow:
1. Page loads with current year heatmap
2. Data source filter works and reloads data
3. Scrolling loads older years
4. Tooltip shows on hover
5. Clicking a cell opens detail panel
6. Detail panel shows per-type counts with labels
7. "View Day in Review" button navigates correctly
8. Month labels navigate to calendar
9. Color scale reflects average glucose correctly

**Step 6: Commit**

```bash
git add -A src/Web/packages/app/src/routes/reports/data-overview/
git add src/Web/packages/app/src/lib/components/reports/CalendarHeatmap.svelte
git commit -m "feat: polish data overview with empty states, legend, and responsive layout"
```

---

## Summary

| Task | What | Files |
|------|------|-------|
| 1 | DTOs | `Core.Models/Services/DataOverviewModels.cs` |
| 2 | Service interface | `Core.Contracts/IDataOverviewService.cs` |
| 3 | Service implementation | `API/Services/DataOverviewService.cs` |
| 4 | Controller + DI registration | `API/Controllers/V4/DataOverviewController.cs`, `Program.cs` |
| 5 | Unit tests | `tests/Unit/Nocturne.API.Tests/Services/DataOverviewServiceTests.cs` |
| 6 | NSwag regeneration | Generated files (aspire run) |
| 7 | Frontend page + detail panel | `routes/reports/data-overview/+page.svelte` |
| 8 | LayerChart calendar heatmap | `components/reports/CalendarHeatmap.svelte` |
| 9 | Navigation (month/week/day) | CalendarHeatmap modifications |
| 10 | Reports index integration | `routes/reports/+page.svelte` |
| 11 | Polish + edge cases | Multiple frontend files |

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Services;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services;

/// <summary>
/// Service for aggregating data overview statistics across all data types.
/// Provides year-level availability and day-level record counts for heatmap visualization.
/// </summary>
public class DataOverviewService : IDataOverviewService
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<DataOverviewService> _logger;

    private const long MillsPerDay = 86_400_000L;

    public DataOverviewService(
        NocturneDbContext context,
        ILogger<DataOverviewService> logger
    )
    {
        _context = context;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DataOverviewYearsResponse> GetAvailableYearsAsync(
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting available years for data overview");

        var minMaxTasks = new List<Task<(long? Min, long? Max)>>();
        var dataSourceTasks = new List<Task<List<string>>>();

        // V4 tables with Mills + DataSource
        minMaxTasks.Add(GetMinMaxMills(
            _context.SensorGlucose.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.MeterGlucose.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.Boluses.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.CarbIntakes.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.BolusCalculations.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.Notes.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.DeviceEvents.Select(e => (long?)e.Mills), cancellationToken));

        // StateSpans uses StartMills
        minMaxTasks.Add(GetMinMaxMills(
            _context.StateSpans.Select(e => (long?)e.StartMills), cancellationToken));

        // Tables without DataSource
        minMaxTasks.Add(GetMinMaxMills(
            _context.Activities.Select(e => (long?)e.Mills), cancellationToken));
        minMaxTasks.Add(GetMinMaxMills(
            _context.DeviceStatuses.Select(e => (long?)e.Mills), cancellationToken));

        // Legacy tables
        minMaxTasks.Add(GetMinMaxMills(
            _context.Entries.Select(e => (long?)e.Mills), cancellationToken));

        // Collect data sources from tables that have DataSource
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.SensorGlucose.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.MeterGlucose.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.Boluses.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.CarbIntakes.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.BolusCalculations.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.Notes.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.DeviceEvents.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));
        // StateSpans uses Source (not DataSource)
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.StateSpans.Where(e => e.Source != null).Select(e => e.Source!), cancellationToken));
        // Legacy Entries
        dataSourceTasks.Add(GetDistinctDataSources(
            _context.Entries.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken));

        await Task.WhenAll(
            Task.WhenAll(minMaxTasks),
            Task.WhenAll(dataSourceTasks)
        );

        // Derive year range from all min/max mills
        long? globalMin = null;
        long? globalMax = null;

        foreach (var task in minMaxTasks)
        {
            var (min, max) = task.Result;
            if (min.HasValue && (!globalMin.HasValue || min.Value < globalMin.Value))
                globalMin = min.Value;
            if (max.HasValue && (!globalMax.HasValue || max.Value > globalMax.Value))
                globalMax = max.Value;
        }

        var years = Array.Empty<int>();
        if (globalMin.HasValue && globalMax.HasValue)
        {
            var minYear = DateTimeOffset.FromUnixTimeMilliseconds(globalMin.Value).UtcDateTime.Year;
            var maxYear = DateTimeOffset.FromUnixTimeMilliseconds(globalMax.Value).UtcDateTime.Year;
            years = Enumerable.Range(minYear, maxYear - minYear + 1).ToArray();
        }

        // Merge all data sources
        var allDataSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var task in dataSourceTasks)
        {
            foreach (var ds in task.Result)
                allDataSources.Add(ds);
        }

        return new DataOverviewYearsResponse
        {
            Years = years,
            AvailableDataSources = allDataSources.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    /// <inheritdoc />
    public async Task<DailySummaryResponse> GetDailySummaryAsync(
        int year,
        string? dataSource = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting daily summary for year {Year}, dataSource={DataSource}", year, dataSource);

        var startOfYear = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startOfNextYear = new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startMills = startOfYear.ToUnixTimeMilliseconds();
        var endMills = startOfNextYear.ToUnixTimeMilliseconds();

        var hasDataSourceFilter = !string.IsNullOrWhiteSpace(dataSource);

        // Dictionary keyed by date string "yyyy-MM-dd" -> DailySummaryDay
        var dayMap = new Dictionary<string, DailySummaryDay>();

        // Collect counts from each table
        var tasks = new List<Task>();

        // V4 tables with Mills + DataSource
        tasks.Add(CollectCountsFromMillsTable(
            "Glucose",
            _context.SensorGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "ManualBG",
            _context.MeterGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "Boluses",
            _context.Boluses
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "CarbIntake",
            _context.CarbIntakes
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "BolusCalculations",
            _context.BolusCalculations
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "Notes",
            _context.Notes
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "DeviceEvents",
            _context.DeviceEvents
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        // StateSpans: uses StartMills and Source (not Mills/DataSource)
        tasks.Add(CollectCountsFromMillsTable(
            "StateSpans",
            _context.StateSpans
                .Where(e => e.StartMills >= startMills && e.StartMills < endMills)
                .Where(e => !hasDataSourceFilter || e.Source == dataSource)
                .Select(e => e.StartMills),
            dayMap, cancellationToken));

        // Activities: has Mills but NO DataSource - skip when filter is active
        if (!hasDataSourceFilter)
        {
            tasks.Add(CollectCountsFromMillsTable(
                "Activity",
                _context.Activities
                    .Where(e => e.Mills >= startMills && e.Mills < endMills)
                    .Select(e => e.Mills),
                dayMap, cancellationToken));
        }

        // DeviceStatuses: has Mills but NO DataSource - skip when filter is active
        if (!hasDataSourceFilter)
        {
            tasks.Add(CollectCountsFromMillsTable(
                "DeviceStatus",
                _context.DeviceStatuses
                    .Where(e => e.Mills >= startMills && e.Mills < endMills)
                    .Select(e => e.Mills),
                dayMap, cancellationToken));
        }

        // Legacy Entries: type "sgv" -> "Glucose", type "mbg" -> "ManualBG"
        tasks.Add(CollectCountsFromMillsTable(
            "Glucose",
            _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "sgv")
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        tasks.Add(CollectCountsFromMillsTable(
            "ManualBG",
            _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "mbg")
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => e.Mills),
            dayMap, cancellationToken));

        // Glucose averages from SensorGlucose + legacy Entries (type=sgv)
        tasks.Add(CollectGlucoseAverages(startMills, endMills, dataSource, hasDataSourceFilter, dayMap, cancellationToken));

        await Task.WhenAll(tasks);

        // Compute TotalCount for each day
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
    /// Gets min and max from an IQueryable of nullable longs, with exception handling per table.
    /// </summary>
    private async Task<(long? Min, long? Max)> GetMinMaxMills(
        IQueryable<long?> millsQuery,
        CancellationToken cancellationToken)
    {
        try
        {
            var min = await millsQuery.MinAsync(cancellationToken);
            var max = await millsQuery.MaxAsync(cancellationToken);
            return (min, max);
        }
        catch (InvalidOperationException)
        {
            // Table is empty - Min/Max on empty sequence
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get min/max mills from table");
            return (null, null);
        }
    }

    /// <summary>
    /// Gets distinct non-null data source values from a query, with exception handling.
    /// </summary>
    private async Task<List<string>> GetDistinctDataSources(
        IQueryable<string> dataSourceQuery,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dataSourceQuery.Distinct().ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get distinct data sources from table");
            return [];
        }
    }

    /// <summary>
    /// Materializes mills values from a table, groups by date in-memory, and merges counts
    /// into the shared dayMap. Thread-safe via lock since multiple tasks write concurrently.
    /// </summary>
    private async Task CollectCountsFromMillsTable(
        string dataType,
        IQueryable<long> millsQuery,
        Dictionary<string, DailySummaryDay> dayMap,
        CancellationToken cancellationToken)
    {
        try
        {
            var millsList = await millsQuery.ToListAsync(cancellationToken);

            var grouped = millsList
                .GroupBy(m => MillsToDateString(m))
                .Select(g => new { Date = g.Key, Count = g.Count() });

            lock (dayMap)
            {
                foreach (var group in grouped)
                {
                    if (!dayMap.TryGetValue(group.Date, out var day))
                    {
                        day = new DailySummaryDay { Date = group.Date };
                        dayMap[group.Date] = day;
                    }

                    day.Counts.TryGetValue(dataType, out var existing);
                    day.Counts[dataType] = existing + group.Count;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect counts for {DataType}", dataType);
        }
    }

    /// <summary>
    /// Collects glucose averages from SensorGlucose and legacy Entries (type=sgv),
    /// computes per-day average Mgdl, and merges into dayMap.
    /// </summary>
    private async Task CollectGlucoseAverages(
        long startMills,
        long endMills,
        string? dataSource,
        bool hasDataSourceFilter,
        Dictionary<string, DailySummaryDay> dayMap,
        CancellationToken cancellationToken)
    {
        try
        {
            // Materialize (mills, mgdl) pairs from SensorGlucose
            var sensorReadings = await _context.SensorGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            // Materialize (mills, mgdl) pairs from legacy Entries where type=sgv
            var legacyReadings = await _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "sgv")
                .Where(e => !hasDataSourceFilter || e.DataSource == dataSource)
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            // Combine and group by date, compute average
            var allReadings = sensorReadings
                .Concat(legacyReadings)
                .GroupBy(r => MillsToDateString(r.Mills))
                .Select(g => new { Date = g.Key, AvgMgdl = g.Average(r => r.Mgdl) });

            lock (dayMap)
            {
                foreach (var group in allReadings)
                {
                    if (!dayMap.TryGetValue(group.Date, out var day))
                    {
                        day = new DailySummaryDay { Date = group.Date };
                        dayMap[group.Date] = day;
                    }

                    // Round to 1 decimal place for cleaner output
                    day.AverageGlucoseMgdl = Math.Round(group.AvgMgdl, 1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect glucose averages");
        }
    }

    /// <summary>
    /// Converts Unix milliseconds to a UTC date string in "yyyy-MM-dd" format.
    /// </summary>
    private static string MillsToDateString(long mills)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime.ToString("yyyy-MM-dd");
    }
}

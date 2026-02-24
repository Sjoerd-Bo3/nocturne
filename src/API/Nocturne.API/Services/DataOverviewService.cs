using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
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

        // Run all queries sequentially — DbContext is not thread-safe
        var minMaxResults = new List<(long? Min, long? Max)>();

        // V4 tables with Mills + DataSource
        minMaxResults.Add(await GetMinMaxMills(
            _context.SensorGlucose.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.MeterGlucose.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.Boluses.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.CarbIntakes.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.BolusCalculations.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.Notes.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.DeviceEvents.Select(e => (long?)e.Mills), cancellationToken));

        // StateSpans uses StartMills
        minMaxResults.Add(await GetMinMaxMills(
            _context.StateSpans.Select(e => (long?)e.StartMills), cancellationToken));

        // Tables without DataSource
        minMaxResults.Add(await GetMinMaxMills(
            _context.Activities.Select(e => (long?)e.Mills), cancellationToken));
        minMaxResults.Add(await GetMinMaxMills(
            _context.DeviceStatuses.Select(e => (long?)e.Mills), cancellationToken));

        // Legacy tables
        minMaxResults.Add(await GetMinMaxMills(
            _context.Entries.Select(e => (long?)e.Mills), cancellationToken));

        // Collect data sources from tables that have DataSource
        var allDataSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var ds in await GetDistinctDataSources(
            _context.SensorGlucose.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.MeterGlucose.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.Boluses.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.CarbIntakes.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.BolusCalculations.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.Notes.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        foreach (var ds in await GetDistinctDataSources(
            _context.DeviceEvents.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);
        // StateSpans uses Source (not DataSource)
        foreach (var ds in await GetDistinctDataSources(
            _context.StateSpans.Where(e => e.Source != null).Select(e => e.Source!), cancellationToken))
            allDataSources.Add(ds);
        // Legacy Entries
        foreach (var ds in await GetDistinctDataSources(
            _context.Entries.Where(e => e.DataSource != null).Select(e => e.DataSource!), cancellationToken))
            allDataSources.Add(ds);

        // Derive year range from all min/max mills
        long? globalMin = null;
        long? globalMax = null;

        foreach (var (min, max) in minMaxResults)
        {
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

        return new DataOverviewYearsResponse
        {
            Years = years,
            AvailableDataSources = allDataSources.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    /// <inheritdoc />
    public async Task<DailySummaryResponse> GetDailySummaryAsync(
        int year,
        string[]? dataSources = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting daily summary for year {Year}, dataSources={DataSources}",
            year, dataSources != null ? string.Join(",", dataSources) : "(all)");

        var startOfYear = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startOfNextYear = new DateTimeOffset(year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var startMills = startOfYear.ToUnixTimeMilliseconds();
        var endMills = startOfNextYear.ToUnixTimeMilliseconds();

        var hasFilter = dataSources is { Length: > 0 };

        // Dictionary keyed by date string "yyyy-MM-dd" -> DailySummaryDay
        var dayMap = new Dictionary<string, DailySummaryDay>();

        // Run all queries sequentially — DbContext is not thread-safe

        // V4 tables with Mills + DataSource
        await CollectCountsFromMillsTable(
            "Glucose",
            _context.SensorGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "ManualBG",
            _context.MeterGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "Boluses",
            _context.Boluses
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "CarbIntake",
            _context.CarbIntakes
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "BolusCalculations",
            _context.BolusCalculations
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "Notes",
            _context.Notes
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "DeviceEvents",
            _context.DeviceEvents
                .Where(e => e.Mills >= startMills && e.Mills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        // StateSpans: uses StartMills and Source (not Mills/DataSource)
        await CollectCountsFromMillsTable(
            "StateSpans",
            _context.StateSpans
                .Where(e => e.StartMills >= startMills && e.StartMills < endMills)
                .Where(e => !hasFilter || dataSources!.Contains(e.Source!))
                .Select(e => e.StartMills),
            dayMap, cancellationToken);

        // Activities: has Mills but NO DataSource - skip when filter is active
        if (!hasFilter)
        {
            await CollectCountsFromMillsTable(
                "Activity",
                _context.Activities
                    .Where(e => e.Mills >= startMills && e.Mills < endMills)
                    .Select(e => e.Mills),
                dayMap, cancellationToken);
        }

        // DeviceStatuses: has Mills but NO DataSource - skip when filter is active
        if (!hasFilter)
        {
            await CollectCountsFromMillsTable(
                "DeviceStatus",
                _context.DeviceStatuses
                    .Where(e => e.Mills >= startMills && e.Mills < endMills)
                    .Select(e => e.Mills),
                dayMap, cancellationToken);
        }

        // Legacy Entries: type "sgv" -> "Glucose", type "mbg" -> "ManualBG"
        await CollectCountsFromMillsTable(
            "Glucose",
            _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "sgv")
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        await CollectCountsFromMillsTable(
            "ManualBG",
            _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "mbg")
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => e.Mills),
            dayMap, cancellationToken);

        // Glucose averages (SensorGlucose + MeterGlucose + legacy Entries)
        await CollectGlucoseAverages(startMills, endMills, dataSources, hasFilter, dayMap, cancellationToken);

        // Insulin totals (Bolus from Boluses table + Basal from Boluses & StateSpans)
        await CollectInsulinTotals(startMills, endMills, dataSources, hasFilter, dayMap, cancellationToken);

        // Carb totals
        await CollectCarbTotals(startMills, endMills, dataSources, hasFilter, dayMap, cancellationToken);

        // Compute TotalCount and TotalDailyDose for each day
        foreach (var day in dayMap.Values)
        {
            day.TotalCount = day.Counts.Values.Sum();

            if (day.TotalBolusUnits.HasValue || day.TotalBasalUnits.HasValue)
            {
                day.TotalDailyDose = (day.TotalBolusUnits ?? 0) + (day.TotalBasalUnits ?? 0);
            }
        }

        return new DailySummaryResponse
        {
            Year = year,
            DataSources = dataSources,
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
    /// Materializes mills values from a table, groups by date in-memory, and merges counts into the dayMap.
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect counts for {DataType}", dataType);
        }
    }

    /// <summary>
    /// Collects glucose averages from SensorGlucose, MeterGlucose, and legacy Entries (type=sgv/mbg).
    /// Each source is queried independently so one failure doesn't prevent the others.
    /// </summary>
    private async Task CollectGlucoseAverages(
        long startMills,
        long endMills,
        string[]? dataSources,
        bool hasFilter,
        Dictionary<string, DailySummaryDay> dayMap,
        CancellationToken cancellationToken)
    {
        // Collect readings from multiple sources independently
        var allReadings = new List<(long Mills, double Mgdl)>();

        // SensorGlucose (CGM)
        try
        {
            var sensorReadings = await _context.SensorGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Mgdl > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            allReadings.AddRange(sensorReadings.Select(r => (r.Mills, r.Mgdl)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect glucose averages from SensorGlucose");
        }

        // MeterGlucose (finger sticks)
        try
        {
            var meterReadings = await _context.MeterGlucose
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Mgdl > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            allReadings.AddRange(meterReadings.Select(r => (r.Mills, r.Mgdl)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect glucose averages from MeterGlucose");
        }

        // Legacy Entries (type=sgv)
        try
        {
            var legacySgv = await _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "sgv" && e.Mgdl > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            allReadings.AddRange(legacySgv.Select(r => (r.Mills, r.Mgdl)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect glucose averages from Entries (sgv)");
        }

        // Legacy Entries (type=mbg)
        try
        {
            var legacyMbg = await _context.Entries
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Type == "mbg" && e.Mgdl > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Mgdl })
                .ToListAsync(cancellationToken);

            allReadings.AddRange(legacyMbg.Select(r => (r.Mills, r.Mgdl)));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect glucose averages from Entries (mbg)");
        }

        if (allReadings.Count == 0)
        {
            _logger.LogDebug("No glucose readings found for year range {StartMills}-{EndMills}", startMills, endMills);
            return;
        }

        // Group by date and compute daily averages
        var grouped = allReadings
            .GroupBy(r => MillsToDateString(r.Mills))
            .Select(g => new { Date = g.Key, AvgMgdl = g.Average(r => r.Mgdl) });

        foreach (var group in grouped)
        {
            if (!dayMap.TryGetValue(group.Date, out var day))
            {
                day = new DailySummaryDay { Date = group.Date };
                dayMap[group.Date] = day;
            }

            day.AverageGlucoseMgdl = Math.Round(group.AvgMgdl, 1);
        }
    }

    /// <summary>
    /// Collects insulin totals from the Boluses table and from BasalDelivery StateSpans
    /// (pump basal delivery with pre-calculated insulin).
    /// </summary>
    private async Task CollectInsulinTotals(
        long startMills,
        long endMills,
        string[]? dataSources,
        bool hasFilter,
        Dictionary<string, DailySummaryDay> dayMap,
        CancellationToken cancellationToken)
    {
        // Bolus records — all boluses are summed into TotalBolusUnits
        // TODO: Query micro_boluses table for basal insulin (Task 14)
        try
        {
            var bolusRecords = await _context.Boluses
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Insulin > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Insulin })
                .ToListAsync(cancellationToken);

            if (bolusRecords.Count > 0)
            {
                var grouped = bolusRecords
                    .GroupBy(r => MillsToDateString(r.Mills))
                    .Select(g => new
                    {
                        Date = g.Key,
                        BolusUnits = g.Sum(r => r.Insulin),
                    });

                foreach (var group in grouped)
                {
                    if (!dayMap.TryGetValue(group.Date, out var day))
                    {
                        day = new DailySummaryDay { Date = group.Date };
                        dayMap[group.Date] = day;
                    }

                    if (group.BolusUnits > 0)
                        day.TotalBolusUnits = Math.Round(group.BolusUnits, 2);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect bolus insulin totals");
        }

        // Basal delivery from StateSpans (pump-confirmed basal rates with calculatedInsulin metadata)
        try
        {
            var basalCategory = nameof(StateSpanCategory.BasalDelivery);
            var basalSpans = await _context.StateSpans
                .Where(e => e.Category == basalCategory
                    && e.StartMills >= startMills && e.StartMills < endMills
                    && e.MetadataJson != null)
                .Where(e => !hasFilter || dataSources!.Contains(e.Source!))
                .Select(e => new { e.StartMills, e.MetadataJson })
                .ToListAsync(cancellationToken);

            if (basalSpans.Count == 0) return;

            var basalByDate = basalSpans
                .Select(s =>
                {
                    var insulin = ExtractCalculatedInsulin(s.MetadataJson!);
                    return new { Date = MillsToDateString(s.StartMills), Insulin = insulin };
                })
                .Where(s => s.Insulin > 0)
                .GroupBy(s => s.Date)
                .Select(g => new { Date = g.Key, TotalBasal = g.Sum(s => s.Insulin) });

            foreach (var group in basalByDate)
            {
                if (!dayMap.TryGetValue(group.Date, out var day))
                {
                    day = new DailySummaryDay { Date = group.Date };
                    dayMap[group.Date] = day;
                }

                // Add to any existing basal from Boluses (APS micro-boluses)
                day.TotalBasalUnits = Math.Round((day.TotalBasalUnits ?? 0) + group.TotalBasal, 2);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect basal delivery from StateSpans");
        }
    }

    /// <summary>
    /// Extracts the calculatedInsulin value from a StateSpan's metadata JSON.
    /// </summary>
    private static double ExtractCalculatedInsulin(string metadataJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadataJson);
            if (doc.RootElement.TryGetProperty("calculatedInsulin", out var prop))
            {
                return prop.GetDouble();
            }
        }
        catch
        {
            // Malformed JSON or missing property — skip
        }

        return 0;
    }

    /// <summary>
    /// Collects total carbs consumed per day from the CarbIntakes table.
    /// </summary>
    private async Task CollectCarbTotals(
        long startMills,
        long endMills,
        string[]? dataSources,
        bool hasFilter,
        Dictionary<string, DailySummaryDay> dayMap,
        CancellationToken cancellationToken)
    {
        try
        {
            var carbRecords = await _context.CarbIntakes
                .Where(e => e.Mills >= startMills && e.Mills < endMills && e.Carbs > 0)
                .Where(e => !hasFilter || dataSources!.Contains(e.DataSource!))
                .Select(e => new { e.Mills, e.Carbs })
                .ToListAsync(cancellationToken);

            if (carbRecords.Count == 0) return;

            var grouped = carbRecords
                .GroupBy(r => MillsToDateString(r.Mills))
                .Select(g => new { Date = g.Key, TotalCarbs = g.Sum(r => r.Carbs) });

            foreach (var group in grouped)
            {
                if (!dayMap.TryGetValue(group.Date, out var day))
                {
                    day = new DailySummaryDay { Date = group.Date };
                    dayMap[group.Date] = day;
                }

                day.TotalCarbs = Math.Round(group.TotalCarbs, 1);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect carb totals");
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

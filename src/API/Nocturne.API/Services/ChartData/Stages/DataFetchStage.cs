using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services.ChartData.Stages;

/// <summary>
/// Pipeline stage that fetches all raw data required for the dashboard chart.
/// All repository calls are sequential because the underlying DbContext is not thread-safe.
/// </summary>
internal sealed class DataFetchStage(
    ISensorGlucoseRepository sensorGlucoseRepository,
    IBolusRepository bolusRepository,
    ICarbIntakeRepository carbIntakeRepository,
    IBGCheckRepository bgCheckRepository,
    IDeviceEventRepository deviceEventRepository,
    ITempBasalRepository tempBasalRepository,
    IStateSpanRepository stateSpanRepository,
    ISystemEventRepository systemEventRepository,
    ITrackerRepository trackerRepository,
    IDeviceStatusService deviceStatusService,
    ILogger<DataFetchStage> logger
) : IChartDataStage
{
    public async Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken)
    {
        var startTime = context.StartTime;
        var endTime = context.EndTime;
        var bufferStartTime = context.BufferStartTime;

        // Helper to convert mills to DateTime for V4 repository calls
        static DateTime? MillsToDateTime(long mills) => DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime;

        // Calculate reasonable limits based on the actual time range
        var rangeHours = (endTime - startTime) / (60.0 * 60 * 1000);
        // At 5-min CGM intervals: ~12 entries/hour. Add 50% safety margin.
        var entryLimit = (int)Math.Max(500, Math.Ceiling(rangeHours * 12 * 1.5));
        // Treatments are less frequent but include the buffer window
        var bufferMs = startTime - bufferStartTime;
        var treatmentRangeHours = (endTime - (startTime - bufferMs)) / (60.0 * 60 * 1000);
        var treatmentLimit = (int)Math.Max(500, Math.Ceiling(treatmentRangeHours * 10));
        var displayRangeLimit = (int)Math.Max(500, Math.Ceiling(rangeHours * 10));

        // Fetch glucose data from v4 SensorGlucose table
        var sensorGlucoseList = (
            await sensorGlucoseRepository.GetAsync(
                from: MillsToDateTime(startTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: entryLimit,
                offset: 0,
                descending: true,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch bolus data from v4 Bolus table — extended range for IOB calculation
        var bolusList = (
            await bolusRepository.GetAsync(
                from: MillsToDateTime(bufferStartTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: treatmentLimit,
                offset: 0,
                descending: true,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch carb data from v4 CarbIntake table — extended range for COB calculation
        var carbIntakeList = (
            await carbIntakeRepository.GetAsync(
                from: MillsToDateTime(bufferStartTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: treatmentLimit,
                offset: 0,
                descending: true,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch BG checks from v4 BGCheck table (display range only)
        var bgCheckList = (
            await bgCheckRepository.GetAsync(
                from: MillsToDateTime(startTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: treatmentLimit,
                offset: 0,
                descending: true,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch device events from v4 DeviceEvent table (display range only)
        var deviceEventList = (
            await deviceEventRepository.GetAsync(
                from: MillsToDateTime(startTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: displayRangeLimit,
                offset: 0,
                descending: true,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch TempBasal records from v4 table (ascending — needed for basal series building)
        var tempBasalList = (await tempBasalRepository.GetAsync(
            from: MillsToDateTime(startTime),
            to: MillsToDateTime(endTime),
            device: null,
            source: null,
            limit: displayRangeLimit,
            offset: 0,
            descending: false,
            ct: cancellationToken
        )).ToList();

        // Fetch all state spans in a single batched query
        var stateSpanCategories = new[]
        {
            StateSpanCategory.PumpMode,
            StateSpanCategory.Profile,
            StateSpanCategory.Override,
            StateSpanCategory.Sleep,
            StateSpanCategory.Exercise,
            StateSpanCategory.Illness,
            StateSpanCategory.Travel,
        };

        var allStateSpans = await stateSpanRepository.GetByCategories(
            stateSpanCategories,
            MillsToDateTime(startTime),
            MillsToDateTime(endTime),
            cancellationToken
        );

        // System events
        var systemEventsResult = await systemEventRepository.GetSystemEventsAsync(
            eventType: null,
            category: null,
            from: startTime,
            to: endTime,
            source: null,
            count: 500,
            skip: 0,
            cancellationToken: cancellationToken
        );

        // Tracker data
        var trackerDefs = await trackerRepository.GetAllDefinitionsAsync(cancellationToken);
        var trackerInstances = await trackerRepository.GetActiveInstancesAsync(
            userId: null,
            cancellationToken: cancellationToken
        );

        // Device status - only need recent entries for IOB source detection
        var deviceStatusList =
            (
                await deviceStatusService.GetDeviceStatusAsync(
                    count: 100,
                    skip: 0,
                    cancellationToken: cancellationToken
                )
            )?.ToList() ?? new List<DeviceStatus>();

        // Display-range subsets for markers
        var displayBoluses = bolusList
            .Where(b => b.Mills >= startTime && b.Mills <= endTime)
            .ToList();
        var displayCarbIntakes = carbIntakeList
            .Where(c => c.Mills >= startTime && c.Mills <= endTime)
            .ToList();

        logger.LogDebug(
            "DataFetchStage: fetched {Glucose} glucose, {Bolus} bolus, {Carb} carb, {BgCheck} bg-check, {DeviceEvent} device-event, {TempBasal} temp-basal, {DeviceStatus} device-status records",
            sensorGlucoseList.Count,
            bolusList.Count,
            carbIntakeList.Count,
            bgCheckList.Count,
            deviceEventList.Count,
            tempBasalList.Count,
            deviceStatusList.Count
        );

        // Project Dictionary<K, List<V>> to IReadOnlyDictionary<K, IEnumerable<V>>
        var stateSpansReadOnly = allStateSpans
            .ToDictionary(
                kvp => kvp.Key,
                kvp => (IEnumerable<StateSpan>)kvp.Value
            );

        return context with
        {
            SensorGlucoseList = sensorGlucoseList,
            BolusList = bolusList,
            DisplayBoluses = displayBoluses,
            CarbIntakeList = carbIntakeList,
            DisplayCarbIntakes = displayCarbIntakes,
            BgCheckList = bgCheckList,
            DeviceEventList = deviceEventList,
            TempBasalList = tempBasalList,
            StateSpans = stateSpansReadOnly,
            SystemEvents = systemEventsResult?.ToList() ?? [],
            TrackerDefinitions = trackerDefs?.ToList() ?? [],
            TrackerInstances = trackerInstances?.ToList() ?? [],
            DeviceStatusList = deviceStatusList,
        };
    }
}

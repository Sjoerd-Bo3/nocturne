using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Nocturne.API.Helpers;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Service that orchestrates all data fetching and computation for the dashboard chart.
/// Loads profiles, fetches glucose/bolus/carb/bg-check/device-event data from v4 tables,
/// builds Treatment adapters for IOB/COB computation, state spans, and assembles the final DTO.
/// All data flows through v4 repositories.
/// </summary>
public class ChartDataService : IChartDataService
{
    private readonly IIobService _iobService;
    private readonly ICobService _cobService;
    private readonly ITreatmentFoodService _treatmentFoodService;
    private readonly IDeviceStatusService _deviceStatusService;
    private readonly IProfileService _profileService;
    private readonly IProfileDataService _profileDataService;
    private readonly ISensorGlucoseRepository _sensorGlucoseRepository;
    private readonly IBolusRepository _bolusRepository;
    private readonly ICarbIntakeRepository _carbIntakeRepository;
    private readonly IBGCheckRepository _bgCheckRepository;
    private readonly IDeviceEventRepository _deviceEventRepository;
    private readonly ITempBasalRepository _tempBasalRepository;
    private readonly StateSpanRepository _stateSpanRepository;
    private readonly SystemEventRepository _systemEventRepository;
    private readonly TrackerRepository _trackerRepository;
    private readonly IMemoryCache _cache;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ChartDataService> _logger;

    // Clinical standard thresholds (mg/dL) -- used when profile doesn't specify
    private const double DefaultVeryLow = 54;
    private const double DefaultVeryHigh = 250;

    // Cache settings
    private static readonly TimeSpan IobCobCacheExpiration = TimeSpan.FromMinutes(1);

    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public ChartDataService(
        IIobService iobService,
        ICobService cobService,
        ITreatmentFoodService treatmentFoodService,
        IDeviceStatusService deviceStatusService,
        IProfileService profileService,
        IProfileDataService profileDataService,
        ISensorGlucoseRepository sensorGlucoseRepository,
        IBolusRepository bolusRepository,
        ICarbIntakeRepository carbIntakeRepository,
        IBGCheckRepository bgCheckRepository,
        IDeviceEventRepository deviceEventRepository,
        ITempBasalRepository tempBasalRepository,
        StateSpanRepository stateSpanRepository,
        SystemEventRepository systemEventRepository,
        TrackerRepository trackerRepository,
        IMemoryCache cache,
        ITenantAccessor tenantAccessor,
        ILogger<ChartDataService> logger
    )
    {
        _iobService = iobService;
        _cobService = cobService;
        _treatmentFoodService = treatmentFoodService;
        _deviceStatusService = deviceStatusService;
        _profileService = profileService;
        _profileDataService = profileDataService;
        _sensorGlucoseRepository = sensorGlucoseRepository;
        _bolusRepository = bolusRepository;
        _carbIntakeRepository = carbIntakeRepository;
        _bgCheckRepository = bgCheckRepository;
        _deviceEventRepository = deviceEventRepository;
        _tempBasalRepository = tempBasalRepository;
        _stateSpanRepository = stateSpanRepository;
        _systemEventRepository = systemEventRepository;
        _trackerRepository = trackerRepository;
        _cache = cache;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<DashboardChartData> GetDashboardChartDataAsync(
        long startTime,
        long endTime,
        int intervalMinutes,
        CancellationToken cancellationToken = default
    )
    {
        // Load profile data
        var profiles = await _profileDataService.GetProfilesAsync(
            count: 100,
            cancellationToken: cancellationToken
        );
        var profileList = profiles?.ToList() ?? new List<Profile>();
        if (profileList.Any())
        {
            _profileService.LoadData(profileList);
            _logger.LogDebug("Loaded {Count} profiles into profile service", profileList.Count);
        }

        // Get profile-based configuration
        var timezone = _profileService.HasData() ? _profileService.GetTimezone() : null;
        var thresholds = GetProfileThresholds(endTime);

        // Helper to convert mills to DateTime for V4 repository calls
        static DateTime? MillsToDateTime(long mills) => DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime;

        // Fetch all data sequentially (DbContext is not thread-safe)
        var bufferMs = 8L * 60 * 60 * 1000; // 8 hours buffer for IOB calculation

        // Calculate reasonable limits based on the actual time range
        var rangeHours = (endTime - startTime) / (60.0 * 60 * 1000);
        // At 5-min CGM intervals: ~12 entries/hour. Add 50% safety margin.
        var entryLimit = (int)Math.Max(500, Math.Ceiling(rangeHours * 12 * 1.5));
        // Treatments are less frequent but include the buffer window
        var treatmentRangeHours = (endTime - (startTime - bufferMs)) / (60.0 * 60 * 1000);
        var treatmentLimit = (int)Math.Max(500, Math.Ceiling(treatmentRangeHours * 10));

        // Fetch glucose data from v4 SensorGlucose table
        var sensorGlucoseList = (
            await _sensorGlucoseRepository.GetAsync(
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
            await _bolusRepository.GetAsync(
                from: MillsToDateTime(startTime - bufferMs),
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
            await _carbIntakeRepository.GetAsync(
                from: MillsToDateTime(startTime - bufferMs),
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
            await _bgCheckRepository.GetAsync(
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

        // Build Treatment adapter objects from v4 Bolus + CarbIntake for IOB/COB computation.
        // The IOB/COB services (IIobService, ICobService) expect List<Treatment> — their interfaces
        // are deeply coupled to the legacy Treatment type. Rather than rewriting those calculation
        // engines, we build thin Treatment adapters containing only the fields they actually use.
        // Fat is derived from food breakdown (TreatmentFood → Food) for COB absorption adjustments.
        var allCarbIntakeIds = carbIntakeList.Select(c => c.Id).ToList();
        var allTreatmentFoods = await _treatmentFoodService.GetByCarbIntakeIdsAsync(
            allCarbIntakeIds,
            cancellationToken
        );
        var foodsByCarbIntake = allTreatmentFoods
            .GroupBy(f => f.CarbIntakeId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var syntheticTreatments = BuildTreatmentsFromV4Data(
            bolusList,
            carbIntakeList,
            foodsByCarbIntake
        );

        // Fetch device events from v4 DeviceEvent table (display range only)
        var displayRangeLimit = (int)Math.Max(500, Math.Ceiling(rangeHours * 10));
        var deviceEventList = (
            await _deviceEventRepository.GetAsync(
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

        // Device status - only need recent entries for IOB source detection
        var deviceStatusList =
            (
                await _deviceStatusService.GetDeviceStatusAsync(
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

        // Fetch TempBasal records from v4 table (replaces BasalDelivery StateSpans)
        var tempBasalList = (
            await _tempBasalRepository.GetAsync(
                from: MillsToDateTime(startTime),
                to: MillsToDateTime(endTime),
                device: null,
                source: null,
                limit: displayRangeLimit,
                offset: 0,
                descending: false,
                ct: cancellationToken
            )
        ).ToList();

        // Fetch all state spans in a single batched query (BasalDelivery now comes from TempBasal table)
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

        var allStateSpans = await _stateSpanRepository.GetByCategories(
            stateSpanCategories,
            MillsToDateTime(startTime),
            MillsToDateTime(endTime),
            cancellationToken
        );

        var pumpModeSpansResult = allStateSpans[StateSpanCategory.PumpMode];
        var profileSpansResult = allStateSpans[StateSpanCategory.Profile];
        var overrideSpansResult = allStateSpans[StateSpanCategory.Override];
        var sleepSpansResult = allStateSpans[StateSpanCategory.Sleep];
        var exerciseSpansResult = allStateSpans[StateSpanCategory.Exercise];
        var illnessSpansResult = allStateSpans[StateSpanCategory.Illness];
        var travelSpansResult = allStateSpans[StateSpanCategory.Travel];

        // System events
        var systemEventsResult = await _systemEventRepository.GetSystemEventsAsync(
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
        var trackerDefs = await _trackerRepository.GetAllDefinitionsAsync(cancellationToken);
        var trackerInstances = await _trackerRepository.GetActiveInstancesAsync(
            userId: null,
            cancellationToken: cancellationToken
        );

        // Get default basal rate
        var defaultBasalRate = _profileService.HasData()
            ? _profileService.GetBasalRate(endTime, null)
            : 1.0;

        // Build computed series — IOB/COB uses synthetic Treatment objects built from v4 data
        var (iobSeries, cobSeries, maxIob, maxCob) = BuildIobCobSeries(
            syntheticTreatments,
            deviceStatusList,
            startTime,
            endTime,
            intervalMinutes
        );

        var basalSeries = BuildBasalSeriesFromTempBasals(
            tempBasalList,
            startTime,
            endTime,
            defaultBasalRate
        );
        var maxBasalRate = Math.Max(
            defaultBasalRate * 2.5,
            basalSeries.Any() ? basalSeries.Max(b => b.Rate) : defaultBasalRate
        );

        var (glucoseData, glucoseYMax) = BuildGlucoseData(sensorGlucoseList);

        // Build markers from v4 tables (display range only)
        var bolusMarkers = BuildBolusMarkers(displayBoluses);
        var carbMarkers = BuildCarbMarkers(displayCarbIntakes, timezone);
        var bgCheckMarkers = BuildBgCheckMarkers(bgCheckList);

        // Device event markers from v4 DeviceEvent table
        var deviceEventMarkers = BuildDeviceEventMarkers(deviceEventList);

        // Process food offsets using carb intake IDs
        var carbIntakeIds = displayCarbIntakes.Select(c => c.Id).Distinct().ToList();
        await ProcessFoodOffsetsAsync(
            carbMarkers,
            carbIntakeIds,
            displayCarbIntakes,
            cancellationToken
        );

        // Map state spans
        var pumpModeSpanDtos = MapStateSpans(pumpModeSpansResult, StateSpanCategory.PumpMode);
        var profileSpanDtos = MapStateSpans(profileSpansResult, StateSpanCategory.Profile);
        var overrideSpanDtos = MapStateSpans(overrideSpansResult, StateSpanCategory.Override);

        var activitySpanDtos = new List<ChartStateSpanDto>();
        activitySpanDtos.AddRange(MapStateSpans(sleepSpansResult, StateSpanCategory.Sleep));
        activitySpanDtos.AddRange(MapStateSpans(exerciseSpansResult, StateSpanCategory.Exercise));
        activitySpanDtos.AddRange(MapStateSpans(illnessSpansResult, StateSpanCategory.Illness));
        activitySpanDtos.AddRange(MapStateSpans(travelSpansResult, StateSpanCategory.Travel));

        var basalDeliverySpanDtos = MapBasalDeliverySpans(tempBasalList);
        var tempBasalSpanDtos = MapTempBasalSpans(tempBasalList);
        var systemEventDtos = MapSystemEvents(systemEventsResult);
        var trackerMarkers = MapTrackerMarkers(trackerDefs, trackerInstances, startTime, endTime);

        return new DashboardChartData
        {
            IobSeries = iobSeries,
            CobSeries = cobSeries,
            BasalSeries = basalSeries,
            DefaultBasalRate = defaultBasalRate,
            MaxBasalRate = maxBasalRate,
            MaxIob = Math.Max(3, maxIob),
            MaxCob = Math.Max(30, maxCob),

            GlucoseData = glucoseData,
            Thresholds = thresholds with { GlucoseYMax = glucoseYMax },

            BolusMarkers = bolusMarkers,
            CarbMarkers = carbMarkers,
            DeviceEventMarkers = deviceEventMarkers,
            BgCheckMarkers = bgCheckMarkers,

            PumpModeSpans = pumpModeSpanDtos,
            ProfileSpans = profileSpanDtos,
            OverrideSpans = overrideSpanDtos,
            ActivitySpans = activitySpanDtos,
            TempBasalSpans = tempBasalSpanDtos,
            BasalDeliverySpans = basalDeliverySpanDtos,

            SystemEventMarkers = systemEventDtos,
            TrackerMarkers = trackerMarkers,
        };
    }

    #region Internal Helpers

    /// <summary>
    /// Deduplicates a time-sorted list by removing items within a time window that match a value predicate.
    /// Keeps the first occurrence in each window. Input must be sorted by time ascending.
    /// </summary>
    private static List<T> DeduplicateByWindow<T>(
        List<T> items,
        Func<T, long> getTime,
        Func<T, T, bool> valuesMatch,
        long windowMillis = 30_000
    )
    {
        if (items.Count <= 1)
            return items;

        var result = new List<T>(items.Count);
        foreach (var item in items)
        {
            var isDuplicate = false;
            for (var i = result.Count - 1; i >= 0; i--)
            {
                if (getTime(item) - getTime(result[i]) > windowMillis)
                    break;
                if (valuesMatch(item, result[i]))
                {
                    isDuplicate = true;
                    break;
                }
            }
            if (!isDuplicate)
                result.Add(item);
        }
        return result;
    }

    internal ChartThresholdsDto GetProfileThresholds(long time)
    {
        if (!_profileService.HasData())
        {
            return new ChartThresholdsDto
            {
                VeryLow = DefaultVeryLow,
                Low = 70,
                High = 180,
                VeryHigh = DefaultVeryHigh,
            };
        }

        return new ChartThresholdsDto
        {
            VeryLow = DefaultVeryLow,
            Low = _profileService.GetLowBGTarget(time, null),
            High = _profileService.GetHighBGTarget(time, null),
            VeryHigh = DefaultVeryHigh,
        };
    }

    internal (
        List<TimeSeriesPoint> iobSeries,
        List<TimeSeriesPoint> cobSeries,
        double maxIob,
        double maxCob
    ) BuildIobCobSeries(
        List<Treatment> treatments,
        List<DeviceStatus> deviceStatuses,
        long startTime,
        long endTime,
        int intervalMinutes
    )
    {
        // Generate cache key based on treatment data hash and time range
        var cacheKey = GenerateIobCobCacheKey(treatments, startTime, endTime, intervalMinutes);

        // Try to get from cache
        if (
            _cache.TryGetValue(
                cacheKey,
                out (
                    List<TimeSeriesPoint> iob,
                    List<TimeSeriesPoint> cob,
                    double maxIob,
                    double maxCob
                ) cached
            )
        )
        {
            _logger.LogDebug("IOB/COB cache hit for range {Start}-{End}", startTime, endTime);
            return cached;
        }

        _logger.LogDebug(
            "IOB/COB cache miss, computing for range {Start}-{End}",
            startTime,
            endTime
        );

        var iobSeries = new List<TimeSeriesPoint>();
        var cobSeries = new List<TimeSeriesPoint>();
        var intervalMs = intervalMinutes * 60 * 1000;
        double maxIob = 0,
            maxCob = 0;

        // Pre-compute DIA and COB absorption window for filtering
        var dia = _profileService.HasData() ? _profileService.GetDIA(endTime, null) : 3.0;
        var diaMs = (long)(dia * 60 * 60 * 1000); // DIA in milliseconds
        var cobAbsorptionMs = 6L * 60 * 60 * 1000; // 6 hours for COB absorption

        // Pre-filter treatments with insulin for IOB calculations
        var insulinTreatments = treatments
            .Where(t => t.Insulin.HasValue && t.Insulin.Value > 0)
            .ToList();

        // Pre-filter treatments with carbs for COB calculations
        var carbTreatments = treatments.Where(t => t.Carbs.HasValue && t.Carbs.Value > 0).ToList();

        var profile = _profileService.HasData() ? _profileService : null;

        for (long t = startTime; t <= endTime; t += intervalMs)
        {
            // Filter to only treatments that could still have active IOB at time t
            // A treatment can only contribute IOB if it was given within DIA hours before t
            var relevantIobTreatments = insulinTreatments
                .Where(tr => tr.Mills <= t && tr.Mills >= t - diaMs)
                .ToList();

            var iobResult =
                relevantIobTreatments.Count > 0
                    ? _iobService.FromTreatments(relevantIobTreatments, profile, t, null)
                    : new IobResult { Iob = 0 };

            var iob = iobResult.Iob;
            iobSeries.Add(new TimeSeriesPoint { Timestamp = t, Value = iob });
            if (iob > maxIob)
                maxIob = iob;

            // Filter to only treatments that could still have active COB at time t
            var relevantCobTreatments = carbTreatments
                .Where(tr => tr.Mills <= t && tr.Mills >= t - cobAbsorptionMs)
                .ToList();

            var cobResult =
                relevantCobTreatments.Count > 0
                    ? _cobService.CobTotal(relevantCobTreatments, deviceStatuses, profile, t, null)
                    : new CobResult { Cob = 0 };

            var cob = cobResult.Cob;
            cobSeries.Add(new TimeSeriesPoint { Timestamp = t, Value = cob });
            if (cob > maxCob)
                maxCob = cob;
        }

        // Cache the result
        var result = (iobSeries, cobSeries, maxIob, maxCob);
        _cache.Set(cacheKey, result, IobCobCacheExpiration);

        return result;
    }

    /// <summary>
    /// Generate a cache key for IOB/COB calculations based on treatment fingerprint and time range.
    /// Uses SHA256 of individual treatment mills/insulin/carbs values for collision resistance.
    /// Includes tenant ID to prevent cross-tenant cache leakage.
    /// </summary>
    private string GenerateIobCobCacheKey(
        List<Treatment> treatments,
        long startTime,
        long endTime,
        int intervalMinutes
    )
    {
        // Round start/end times to interval boundaries for better cache hits
        var intervalMs = intervalMinutes * 60 * 1000;
        var roundedStart = (startTime / intervalMs) * intervalMs;
        var roundedEnd = (endTime / intervalMs) * intervalMs;

        // Hash individual treatment data for a collision-resistant fingerprint
        var sb = new StringBuilder();
        foreach (var t in treatments)
        {
            if (
                (t.Insulin.HasValue && t.Insulin.Value > 0)
                || (t.Carbs.HasValue && t.Carbs.Value > 0)
            )
            {
                sb.Append(t.Mills)
                    .Append(':')
                    .Append(t.Insulin ?? 0)
                    .Append(':')
                    .Append(t.Carbs ?? 0)
                    .Append('|');
            }
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[
            ..16
        ]; // First 16 hex chars (64 bits) is sufficient

        return $"iobcob:{TenantCacheId}:{hash}:{roundedStart}:{roundedEnd}:{intervalMinutes}";
    }

    internal static (List<GlucosePointDto> data, double yMax) BuildGlucoseData(
        List<SensorGlucose> readings
    )
    {
        var sorted = readings.OrderBy(r => r.Mills).ToList();
        var deduped = DeduplicateByWindow(
            sorted,
            r => r.Mills,
            (a, b) => Math.Abs(a.Mgdl - b.Mgdl) <= 1.0
        );

        var glucoseData = deduped
            .Select(r => new GlucosePointDto
            {
                Time = r.Mills,
                Sgv = r.Mgdl,
                Direction = r.Direction?.ToString(),
            })
            .ToList(); // Already sorted

        var maxSgv = glucoseData.Any() ? glucoseData.Max(g => g.Sgv) : 280;
        var glucoseYMax = Math.Min(400, Math.Max(280, maxSgv) + 20);

        return (glucoseData, glucoseYMax);
    }

    internal static List<BolusMarkerDto> BuildBolusMarkers(List<Bolus> boluses)
    {
        var sorted = boluses.Where(b => b.Insulin > 0).OrderBy(b => b.Mills).ToList();

        var deduped = DeduplicateByWindow(
            sorted,
            b => b.Mills,
            (a, b) => Math.Abs(a.Insulin - b.Insulin) <= 0.05
        );

        return deduped
            .Select(b => new BolusMarkerDto
            {
                Time = b.Mills,
                Insulin = b.Insulin,
                TreatmentId = b.LegacyId ?? b.Id.ToString(),
                BolusType = MapV4BolusType(b.BolusType, b.Automatic),
                IsOverride = false,
            })
            .ToList();
    }

    internal static List<CarbMarkerDto> BuildCarbMarkers(
        List<CarbIntake> carbIntakes,
        string? timezone
    )
    {
        var sorted = carbIntakes.Where(c => c.Carbs > 0).OrderBy(c => c.Mills).ToList();

        var deduped = DeduplicateByWindow(
            sorted,
            c => c.Mills,
            (a, b) => Math.Abs(a.Carbs - b.Carbs) <= 1.0
        );

        return deduped
            .Select(c => new CarbMarkerDto
            {
                Time = c.Mills,
                Carbs = c.Carbs,
                Label = GetMealNameForTime(c.Mills, timezone),
                TreatmentId = c.LegacyId ?? c.Id.ToString(),
                IsOffset = false,
            })
            .ToList();
    }

    internal static List<DeviceEventMarkerDto> BuildDeviceEventMarkers(
        List<DeviceEvent> deviceEvents
    )
    {
        var sorted = deviceEvents.OrderBy(e => e.Mills).ToList();

        var deduped = DeduplicateByWindow(
            sorted,
            e => e.Mills,
            (a, b) => a.EventType == b.EventType
        );

        return deduped
            .Select(e => new DeviceEventMarkerDto
            {
                Time = e.Mills,
                EventType = e.EventType,
                Notes = e.Notes,
                TreatmentId = e.LegacyId ?? e.Id.ToString(),
                Color = ChartColorMapper.FromDeviceEvent(e.EventType),
            })
            .ToList();
    }

    internal static List<BgCheckMarkerDto> BuildBgCheckMarkers(List<BGCheck> bgChecks)
    {
        var sorted = bgChecks.Where(b => b.Mgdl > 0).OrderBy(b => b.Mills).ToList();

        var deduped = DeduplicateByWindow(
            sorted,
            b => b.Mills,
            (a, b) => Math.Abs(a.Mgdl - b.Mgdl) <= 1.0
        );

        return deduped
            .Select(b => new BgCheckMarkerDto
            {
                Time = b.Mills,
                Glucose = b.Mgdl,
                GlucoseType = b.GlucoseType?.ToString(),
                TreatmentId = b.LegacyId ?? b.Id.ToString(),
            })
            .ToList();
    }

    /// <summary>
    /// Builds lightweight Treatment adapter objects from v4 Bolus and CarbIntake data.
    /// The IOB/COB calculation services (IIobService, ICobService) are deeply coupled to the
    /// legacy Treatment type through their interfaces. Rather than rewriting those calculation
    /// engines (which implement exact 1:1 legacy JavaScript algorithm compatibility), we build
    /// thin Treatment objects containing only the fields the calculations actually use:
    ///   - IOB: Treatment.Mills, Treatment.Insulin, Treatment.EventType ("Temp Basal"),
    ///          Treatment.Duration, Treatment.Absolute
    ///   - COB: Treatment.Mills, Treatment.Carbs, Treatment.Notes
    /// </summary>
    internal static List<Treatment> BuildTreatmentsFromV4Data(
        List<Bolus> boluses,
        List<CarbIntake> carbIntakes,
        IReadOnlyDictionary<Guid, List<TreatmentFood>> foodsByCarbIntake
    )
    {
        var treatments = new List<Treatment>(boluses.Count + carbIntakes.Count);

        foreach (var bolus in boluses)
        {
            if (bolus.Insulin <= 0)
                continue;

            treatments.Add(
                new Treatment
                {
                    Id = bolus.LegacyId ?? bolus.Id.ToString(),
                    Mills = bolus.Mills,
                    Insulin = bolus.Insulin,
                }
            );
        }

        foreach (var carb in carbIntakes)
        {
            if (carb.Carbs <= 0)
                continue;

            double? totalFat = null;
            if (foodsByCarbIntake.TryGetValue(carb.Id, out var foods))
            {
                var sum = foods
                    .Where(f => f.FatPerPortion.HasValue && f.Portions > 0)
                    .Sum(f => (double)(f.FatPerPortion!.Value * f.Portions));
                if (sum > 0)
                    totalFat = sum;
            }

            treatments.Add(
                new Treatment
                {
                    Id = carb.LegacyId ?? carb.Id.ToString(),
                    Mills = carb.Mills,
                    Carbs = carb.Carbs,
                    Fat = totalFat,
                    AbsorptionTime = carb.AbsorptionTime,
                }
            );
        }

        return treatments;
    }

    /// <summary>
    /// Maps v4 BolusType enum to the chart BolusType enum.
    /// The v4 model uses a simpler BolusType (Normal, Square, Dual) plus an Automatic flag,
    /// while the chart uses a more granular BolusType derived from legacy event type strings.
    /// </summary>
    internal static Nocturne.Core.Models.BolusType MapV4BolusType(
        Nocturne.Core.Models.V4.BolusType? v4Type,
        bool automatic
    )
    {
        if (automatic)
            return Nocturne.Core.Models.BolusType.AutomaticBolus;

        return v4Type switch
        {
            Nocturne.Core.Models.V4.BolusType.Square => Nocturne.Core.Models.BolusType.ComboBolus,
            Nocturne.Core.Models.V4.BolusType.Dual => Nocturne.Core.Models.BolusType.ComboBolus,
            _ => Nocturne.Core.Models.BolusType.Bolus,
        };
    }

    internal async Task ProcessFoodOffsetsAsync(
        List<CarbMarkerDto> carbMarkers,
        List<Guid> carbIntakeIds,
        List<CarbIntake> displayCarbIntakes,
        CancellationToken cancellationToken
    )
    {
        if (carbIntakeIds.Count == 0)
            return;

        var foods = (
            await _treatmentFoodService.GetByCarbIntakeIdsAsync(carbIntakeIds, cancellationToken)
        ).ToList();

        if (foods.Count == 0)
            return;

        var foodsByCarbIntake = foods
            .GroupBy(f => f.CarbIntakeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var carbIntakeById = displayCarbIntakes.ToDictionary(c => c.Id, c => c);

        foreach (var (carbIntakeId, carbIntakeFoods) in foodsByCarbIntake)
        {
            var offsetFoods = carbIntakeFoods.Where(f => f.TimeOffsetMinutes != 0).ToList();

            if (offsetFoods.Count == 0)
                continue;

            if (!carbIntakeById.TryGetValue(carbIntakeId, out var baseCarbIntake))
                continue;

            var baseMills = baseCarbIntake.Mills;
            var baseId = baseCarbIntake.LegacyId ?? baseCarbIntake.Id.ToString();
            var offsetGroups = offsetFoods.GroupBy(f => f.TimeOffsetMinutes).ToList();

            foreach (var group in offsetGroups)
            {
                var offsetMs = group.Key * 60 * 1000;
                var offsetTime = baseMills + offsetMs;
                var totalCarbs = group.Sum(f => (double)f.Carbs);
                var labels = group.Where(f => f.FoodName != null).Select(f => f.FoodName!).ToList();
                var label =
                    labels.Count > 0
                        ? string.Join(", ", labels)[
                            ..Math.Min(string.Join(", ", labels).Length, 20)
                        ]
                        : null;

                carbMarkers.Add(
                    new CarbMarkerDto
                    {
                        Time = offsetTime,
                        Carbs = totalCarbs,
                        Label = label,
                        TreatmentId = baseId,
                        IsOffset = true,
                    }
                );
            }

            // Update base marker label with base food names
            var baseFoods = carbIntakeFoods.Where(f => f.TimeOffsetMinutes == 0).ToList();
            if (baseFoods.Count > 0)
            {
                var baseLabels = baseFoods
                    .Where(f => f.FoodName != null)
                    .Select(f => f.FoodName!)
                    .ToList();
                if (baseLabels.Count > 0)
                {
                    var baseMarker = carbMarkers.FirstOrDefault(m =>
                        m.TreatmentId == baseId && !m.IsOffset
                    );
                    if (baseMarker != null)
                    {
                        var joined = string.Join(", ", baseLabels);
                        baseMarker.Label = joined[..Math.Min(joined.Length, 20)];
                    }
                }
            }
        }
    }

    internal static List<BasalDeliverySpanDto> MapBasalDeliverySpans(
        List<TempBasal> tempBasals
    )
    {
        return tempBasals
            .Select(tb =>
            {
                var origin = MapTempBasalOrigin(tb.Origin);
                return new BasalDeliverySpanDto
                {
                    Id = tb.LegacyId ?? tb.Id.ToString(),
                    StartMills = tb.StartMills,
                    EndMills = tb.EndMills,
                    Rate = origin == BasalDeliveryOrigin.Suspended ? 0 : tb.Rate,
                    Origin = origin,
                    Source = tb.DataSource,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(origin),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(origin),
                };
            })
            .ToList();
    }

    internal static List<ChartStateSpanDto> MapTempBasalSpans(
        List<TempBasal> tempBasals
    )
    {
        return tempBasals
            .Where(tb => tb.Origin == TempBasalOrigin.Manual)
            .Select(tb => new ChartStateSpanDto
            {
                Id = tb.LegacyId ?? tb.Id.ToString(),
                Category = StateSpanCategory.PumpMode, // Rendered in dedicated tempBasalSpans list; category is informational
                State = "TempBasal",
                StartMills = tb.StartMills,
                EndMills = tb.EndMills,
                Color = ChartColor.InsulinBasal,
                Metadata = null,
            })
            .ToList();
    }

    /// <summary>
    /// Maps a TempBasalOrigin enum value to the corresponding BasalDeliveryOrigin enum value.
    /// Both enums have identical members (Algorithm, Scheduled, Manual, Suspended, Inferred).
    /// </summary>
    internal static BasalDeliveryOrigin MapTempBasalOrigin(TempBasalOrigin origin) =>
        origin switch
        {
            TempBasalOrigin.Algorithm => BasalDeliveryOrigin.Algorithm,
            TempBasalOrigin.Scheduled => BasalDeliveryOrigin.Scheduled,
            TempBasalOrigin.Manual => BasalDeliveryOrigin.Manual,
            TempBasalOrigin.Suspended => BasalDeliveryOrigin.Suspended,
            TempBasalOrigin.Inferred => BasalDeliveryOrigin.Inferred,
            _ => BasalDeliveryOrigin.Scheduled,
        };

    internal static List<SystemEventMarkerDto> MapSystemEvents(
        IEnumerable<SystemEvent>? systemEvents
    )
    {
        return (systemEvents ?? Enumerable.Empty<SystemEvent>())
            .Select(e => new SystemEventMarkerDto
            {
                Id = e.Id ?? "",
                Time = e.Mills,
                EventType = e.EventType,
                Category = e.Category,
                Code = e.Code,
                Description = e.Description,
                Color = ChartColorMapper.FromSystemEvent(e.EventType),
            })
            .ToList();
    }

    internal static List<TrackerMarkerDto> MapTrackerMarkers(
        IEnumerable<TrackerDefinitionEntity> trackerDefs,
        IEnumerable<TrackerInstanceEntity> trackerInstances,
        long startTime,
        long endTime
    )
    {
        var defsList = trackerDefs.ToList();
        return trackerInstances
            .Where(i => i.ExpectedEndAt.HasValue)
            .Where(i =>
            {
                var expectedMills = new DateTimeOffset(
                    i.ExpectedEndAt!.Value,
                    TimeSpan.Zero
                ).ToUnixTimeMilliseconds();
                return expectedMills >= startTime && expectedMills <= endTime;
            })
            .Select(i =>
            {
                var def = defsList.FirstOrDefault(d => d.Id == i.DefinitionId);
                var category = def?.Category ?? TrackerCategory.Custom;
                var expectedMills = new DateTimeOffset(
                    i.ExpectedEndAt!.Value,
                    TimeSpan.Zero
                ).ToUnixTimeMilliseconds();

                return new TrackerMarkerDto
                {
                    Id = i.Id.ToString(),
                    DefinitionId = i.DefinitionId.ToString(),
                    Name = def?.Name ?? "Tracker",
                    Category = category,
                    Time = expectedMills,
                    Icon = def?.Icon,
                    Color = ChartColorMapper.FromTracker(category),
                };
            })
            .OrderBy(m => m.Time)
            .ToList();
    }

    internal List<ChartStateSpanDto> MapStateSpans(
        IEnumerable<StateSpan> spans,
        StateSpanCategory category
    )
    {
        return spans
            .Select(span => new ChartStateSpanDto
            {
                Id = span.Id ?? "",
                Category = category,
                State = span.State ?? "Unknown",
                StartMills = span.StartMills,
                EndMills = span.EndMills,
                Color = category switch
                {
                    StateSpanCategory.PumpMode => ChartColorMapper.FromPumpMode(span.State ?? ""),
                    StateSpanCategory.Override => ChartColorMapper.FromOverride(span.State ?? ""),
                    StateSpanCategory.Profile => ChartColor.Profile,
                    StateSpanCategory.Sleep
                    or StateSpanCategory.Exercise
                    or StateSpanCategory.Illness
                    or StateSpanCategory.Travel => ChartColorMapper.FromActivity(category),
                    _ => ChartColor.MutedForeground,
                },
                Metadata = span.Metadata,
            })
            .ToList();
    }

    internal static (double rate, BasalDeliveryOrigin origin) ExtractBasalDeliveryMetadata(
        StateSpan span,
        double defaultRate
    )
    {
        double rate = defaultRate;
        if (span.Metadata?.TryGetValue("rate", out var rateObj) == true)
        {
            rate = rateObj switch
            {
                JsonElement jsonElement => jsonElement.GetDouble(),
                double d => d,
                _ => Convert.ToDouble(rateObj),
            };
        }

        string? originStr = "Scheduled";
        if (span.Metadata?.TryGetValue("origin", out var originObj) == true)
        {
            originStr = originObj switch
            {
                JsonElement jsonElement => jsonElement.GetString(),
                string s => s,
                _ => originObj?.ToString(),
            };
        }

        var origin = originStr?.ToLowerInvariant() switch
        {
            "algorithm" => BasalDeliveryOrigin.Algorithm,
            "manual" => BasalDeliveryOrigin.Manual,
            "suspended" => BasalDeliveryOrigin.Suspended,
            _ => BasalDeliveryOrigin.Scheduled,
        };

        return (rate, origin);
    }

    internal static string GetMealNameForTime(long mills, string? timezone)
    {
        var time = DateTimeOffset.FromUnixTimeMilliseconds(mills);
        if (!string.IsNullOrEmpty(timezone))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                time = TimeZoneInfo.ConvertTime(time, tz);
            }
            catch
            {
                // Fall back to UTC if timezone conversion fails
            }
        }
        return time.Hour switch
        {
            >= 5 and < 11 => "Breakfast",
            >= 11 and < 15 => "Lunch",
            >= 15 and < 17 => "Snack",
            >= 17 and < 21 => "Dinner",
            _ => "Late Night",
        };
    }

    /// <summary>
    /// Build basal series from BasalDelivery StateSpans.
    /// StateSpans are the source of truth for pump-confirmed delivery.
    /// Falls back to profile-based rates when there are gaps in StateSpan data.
    /// </summary>
    internal List<BasalPoint> BuildBasalSeriesFromStateSpans(
        List<StateSpan> basalDeliverySpans,
        long startTime,
        long endTime,
        double defaultBasalRate
    )
    {
        var series = new List<BasalPoint>();
        var sortedSpans = basalDeliverySpans.OrderBy(s => s.StartMills).ToList();

        _logger.LogDebug(
            "Building basal series from {SpanCount} BasalDelivery StateSpans",
            sortedSpans.Count
        );

        if (sortedSpans.Count == 0)
            return BuildBasalSeriesFromProfile(startTime, endTime, defaultBasalRate);

        long currentTime = startTime;

        foreach (var span in sortedSpans)
        {
            var spanStart = span.StartMills;
            var spanEnd = span.EndMills ?? endTime;

            if (spanEnd < startTime || spanStart > endTime)
                continue;

            spanStart = Math.Max(spanStart, startTime);
            spanEnd = Math.Min(spanEnd, endTime);

            if (spanStart > currentTime)
            {
                series.AddRange(
                    BuildBasalSeriesFromProfile(currentTime, spanStart, defaultBasalRate)
                );
            }

            var (rate, origin) = ExtractBasalDeliveryMetadata(span, defaultBasalRate);

            var scheduledRate = _profileService.HasData()
                ? _profileService.GetBasalRate(spanStart, null)
                : defaultBasalRate;

            series.Add(
                new BasalPoint
                {
                    Timestamp = spanStart,
                    Rate = origin == BasalDeliveryOrigin.Suspended ? 0 : rate,
                    ScheduledRate = scheduledRate,
                    Origin = origin,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(origin),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(origin),
                }
            );

            currentTime = spanEnd;
        }

        if (currentTime < endTime)
            series.AddRange(BuildBasalSeriesFromProfile(currentTime, endTime, defaultBasalRate));

        if (series.Count == 0)
        {
            series.Add(
                new BasalPoint
                {
                    Timestamp = startTime,
                    Rate = defaultBasalRate,
                    ScheduledRate = defaultBasalRate,
                    Origin = BasalDeliveryOrigin.Scheduled,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(BasalDeliveryOrigin.Scheduled),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                        BasalDeliveryOrigin.Scheduled
                    ),
                }
            );
        }

        return series;
    }

    /// <summary>
    /// Build basal series from TempBasal records.
    /// TempBasal records are the v4 source of truth for pump-confirmed basal delivery.
    /// Falls back to profile-based rates when there are gaps in TempBasal data.
    /// </summary>
    internal List<BasalPoint> BuildBasalSeriesFromTempBasals(
        List<TempBasal> tempBasals,
        long startTime,
        long endTime,
        double defaultBasalRate
    )
    {
        var series = new List<BasalPoint>();
        var sorted = tempBasals.OrderBy(tb => tb.StartMills).ToList();

        _logger.LogDebug(
            "Building basal series from {Count} TempBasal records",
            sorted.Count
        );

        if (sorted.Count == 0)
            return BuildBasalSeriesFromProfile(startTime, endTime, defaultBasalRate);

        long currentTime = startTime;

        foreach (var tb in sorted)
        {
            var tbStart = tb.StartMills;
            var tbEnd = tb.EndMills ?? endTime;

            if (tbEnd < startTime || tbStart > endTime)
                continue;

            tbStart = Math.Max(tbStart, startTime);
            tbEnd = Math.Min(tbEnd, endTime);

            if (tbStart > currentTime)
            {
                series.AddRange(
                    BuildBasalSeriesFromProfile(currentTime, tbStart, defaultBasalRate)
                );
            }

            var origin = MapTempBasalOrigin(tb.Origin);

            var scheduledRate = tb.ScheduledRate
                ?? (_profileService.HasData()
                    ? _profileService.GetBasalRate(tbStart, null)
                    : defaultBasalRate);

            series.Add(
                new BasalPoint
                {
                    Timestamp = tbStart,
                    Rate = origin == BasalDeliveryOrigin.Suspended ? 0 : tb.Rate,
                    ScheduledRate = scheduledRate,
                    Origin = origin,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(origin),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(origin),
                }
            );

            currentTime = tbEnd;
        }

        if (currentTime < endTime)
            series.AddRange(BuildBasalSeriesFromProfile(currentTime, endTime, defaultBasalRate));

        if (series.Count == 0)
        {
            series.Add(
                new BasalPoint
                {
                    Timestamp = startTime,
                    Rate = defaultBasalRate,
                    ScheduledRate = defaultBasalRate,
                    Origin = BasalDeliveryOrigin.Scheduled,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(BasalDeliveryOrigin.Scheduled),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                        BasalDeliveryOrigin.Scheduled
                    ),
                }
            );
        }

        return series;
    }

    internal List<BasalPoint> BuildBasalSeriesFromProfile(
        long startTime,
        long endTime,
        double defaultBasalRate
    )
    {
        var series = new List<BasalPoint>();
        const long intervalMs = 5 * 60 * 1000;
        double? prevRate = null;

        for (long t = startTime; t <= endTime; t += intervalMs)
        {
            var rate = _profileService.HasData()
                ? _profileService.GetBasalRate(t, null)
                : defaultBasalRate;

            if (prevRate == null || Math.Abs(rate - prevRate.Value) > 0.001)
            {
                series.Add(
                    new BasalPoint
                    {
                        Timestamp = t,
                        Rate = rate,
                        ScheduledRate = rate,
                        Origin = BasalDeliveryOrigin.Inferred,
                        FillColor = ChartColorMapper.FillFromBasalOrigin(
                            BasalDeliveryOrigin.Inferred
                        ),
                        StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                            BasalDeliveryOrigin.Inferred
                        ),
                    }
                );
                prevRate = rate;
            }
        }

        if (series.Count == 0)
        {
            series.Add(
                new BasalPoint
                {
                    Timestamp = startTime,
                    Rate = defaultBasalRate,
                    ScheduledRate = defaultBasalRate,
                    Origin = BasalDeliveryOrigin.Inferred,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(BasalDeliveryOrigin.Inferred),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                        BasalDeliveryOrigin.Inferred
                    ),
                }
            );
        }

        return series;
    }

    #endregion
}

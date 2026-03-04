using Nocturne.Connectors.Core.Constants;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services;

/// <summary>
/// Projects V4 granular records back into the legacy Entry and Treatment shapes for v1/v2/v3
/// API compatibility.  Only records written directly to V4 tables (i.e. those whose LegacyId is
/// null – they have no corresponding row in the legacy entries/treatments tables) are projected.
/// </summary>
public class V4ToLegacyProjectionService : IV4ToLegacyProjectionService
{
    private readonly ISensorGlucoseRepository _sensorGlucoseRepository;
    private readonly IBolusRepository _bolusRepository;
    private readonly ICarbIntakeRepository _carbIntakeRepository;
    private readonly IBGCheckRepository _bgCheckRepository;
    private readonly INoteRepository _noteRepository;
    private readonly IDeviceEventRepository _deviceEventRepository;
    private readonly ITreatmentFoodService _treatmentFoodService;
    private readonly ILogger<V4ToLegacyProjectionService> _logger;

    // DeviceEventType → legacy Nightscout eventType string (reverse of TreatmentTypes.DeviceEventTypeMap)
    private static readonly Dictionary<DeviceEventType, string> DeviceEventTypeToString =
        new()
        {
            [DeviceEventType.SensorStart] = TreatmentTypes.SensorStart,
            [DeviceEventType.SensorChange] = TreatmentTypes.SensorChange,
            [DeviceEventType.SensorStop] = TreatmentTypes.SensorStop,
            [DeviceEventType.SiteChange] = TreatmentTypes.SiteChange,
            [DeviceEventType.InsulinChange] = TreatmentTypes.InsulinChange,
            [DeviceEventType.PumpBatteryChange] = TreatmentTypes.PumpBatteryChange,
            [DeviceEventType.PodChange] = TreatmentTypes.PodChange,
            [DeviceEventType.ReservoirChange] = TreatmentTypes.ReservoirChange,
            [DeviceEventType.CannulaChange] = TreatmentTypes.CannulaChange,
            [DeviceEventType.TransmitterSensorInsert] = TreatmentTypes.TransmitterSensorInsert,
        };

    public V4ToLegacyProjectionService(
        ISensorGlucoseRepository sensorGlucoseRepository,
        IBolusRepository bolusRepository,
        ICarbIntakeRepository carbIntakeRepository,
        IBGCheckRepository bgCheckRepository,
        INoteRepository noteRepository,
        IDeviceEventRepository deviceEventRepository,
        ITreatmentFoodService treatmentFoodService,
        ILogger<V4ToLegacyProjectionService> logger
    )
    {
        _sensorGlucoseRepository = sensorGlucoseRepository;
        _bolusRepository = bolusRepository;
        _carbIntakeRepository = carbIntakeRepository;
        _bgCheckRepository = bgCheckRepository;
        _noteRepository = noteRepository;
        _deviceEventRepository = deviceEventRepository;
        _treatmentFoodService = treatmentFoodService;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <summary>
    /// Converts nullable unix milliseconds to nullable DateTime.
    /// </summary>
    private static DateTime? MillsToDateTime(long? mills) =>
        mills.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(mills.Value).UtcDateTime : null;

    public async Task<IEnumerable<Entry>> GetProjectedEntriesAsync(
        long? fromMills,
        long? toMills,
        int limit,
        int offset,
        bool descending,
        CancellationToken ct = default
    )
    {
        IEnumerable<SensorGlucose> records;
        try
        {
            records = await _sensorGlucoseRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: offset,
                descending: descending,
                nativeOnly: true,
                ct: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch V4 SensorGlucose records for projection");
            return Enumerable.Empty<Entry>();
        }

        return records.Select(ProjectSensorGlucoseToEntry);
    }

    /// <inheritdoc />
    public async Task<Entry?> GetLatestProjectedEntryAsync(CancellationToken ct = default)
    {
        IEnumerable<SensorGlucose> records;
        try
        {
            records = await _sensorGlucoseRepository.GetAsync(
                from: null,
                to: null,
                device: null,
                source: null,
                limit: 1,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch V4 SensorGlucose records for latest projection");
            return null;
        }

        var latest = records.FirstOrDefault();
        return latest == null ? null : ProjectSensorGlucoseToEntry(latest);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetProjectedTreatmentsAsync(
        long? fromMills,
        long? toMills,
        int limit,
        CancellationToken ct = default
    )
    {
        // Fetch all V4 treatment record types sequentially.
        // These repositories share a scoped DbContext which is not thread-safe,
        // so they cannot be run concurrently via Task.WhenAll.
        var boluses = (await FetchSafe(() =>
            _bolusRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            )
        )).ToList();

        var carbs = (await FetchSafe(() =>
            _carbIntakeRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            )
        )).ToList();

        var bgChecks = (await FetchSafe(() =>
            _bgCheckRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            )
        )).ToList();

        var notes = (await FetchSafe(() =>
            _noteRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            )
        )).ToList();

        var deviceEvents = (await FetchSafe(() =>
            _deviceEventRepository.GetAsync(
                from: MillsToDateTime(fromMills),
                to: MillsToDateTime(toMills),
                device: null,
                source: null,
                limit: limit,
                offset: 0,
                descending: true,
                nativeOnly: true,
                ct: ct
            )
        )).ToList();

        // Load food breakdown entries for all carb intakes to populate legacy fields
        var carbIds = carbs.Select(c => c.Id).ToList();
        var allFoodEntries = carbIds.Count > 0
            ? (await _treatmentFoodService.GetByCarbIntakeIdsAsync(carbIds, ct)).ToList()
            : [];
        var foodsByCarbId = allFoodEntries
            .GroupBy(f => f.CarbIntakeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var treatments = new List<Treatment>();

        // --- Bolus + CarbIntake pairing ---
        // Phase 1: FK-based pairing (preferred for native V4 records)
        var bolusLookup = boluses.ToDictionary(b => b.Id);
        var pairedCarbIds = new HashSet<Guid>();
        var pairedBolusIds = new HashSet<Guid>();

        foreach (var carb in carbs.Where(c => c.BolusId.HasValue))
        {
            if (bolusLookup.TryGetValue(carb.BolusId!.Value, out var bolus))
            {
                pairedCarbIds.Add(carb.Id);
                pairedBolusIds.Add(bolus.Id);
                treatments.Add(ProjectMealBolus(bolus, carb, foodsByCarbId.GetValueOrDefault(carb.Id, [])));
            }
        }

        // Phase 2: CorrelationId-based pairing (fallback for legacy data)
        var remainingBoluses = boluses.Where(b => !pairedBolusIds.Contains(b.Id)).ToList();
        var remainingCarbs = carbs.Where(c => !pairedCarbIds.Contains(c.Id)).ToList();

        var bolusesWithCorrelation = remainingBoluses
            .Where(b => b.CorrelationId.HasValue)
            .ToLookup(b => b.CorrelationId!.Value);
        var bolusesWithoutCorrelation = remainingBoluses.Where(b => !b.CorrelationId.HasValue).ToList();

        var carbsWithCorrelation = remainingCarbs
            .Where(c => c.CorrelationId.HasValue)
            .ToLookup(c => c.CorrelationId!.Value);
        var carbsWithoutCorrelation = remainingCarbs.Where(c => !c.CorrelationId.HasValue).ToList();

        var allCorrelationIds = bolusesWithCorrelation
            .Select(g => g.Key)
            .Union(carbsWithCorrelation.Select(g => g.Key))
            .Distinct();

        foreach (var correlationId in allCorrelationIds)
        {
            var pairedBoluses = bolusesWithCorrelation[correlationId].ToList();
            var pairedCarbs = carbsWithCorrelation[correlationId].ToList();

            if (pairedBoluses.Count > 0 && pairedCarbs.Count > 0)
            {
                var b = pairedBoluses.First();
                var c = pairedCarbs.First();
                pairedCarbIds.Add(c.Id);
                pairedBolusIds.Add(b.Id);
                treatments.Add(ProjectMealBolus(b, c, foodsByCarbId.GetValueOrDefault(c.Id, [])));
            }
            else if (pairedBoluses.Count > 0)
            {
                foreach (var b in pairedBoluses)
                {
                    pairedBolusIds.Add(b.Id);
                    treatments.Add(ProjectCorrectionBolus(b));
                }
            }
            else
            {
                foreach (var c in pairedCarbs)
                {
                    pairedCarbIds.Add(c.Id);
                    treatments.Add(ProjectCarbCorrection(c, foodsByCarbId.GetValueOrDefault(c.Id, [])));
                }
            }
        }

        // Remaining unpaired records
        foreach (var bolus in bolusesWithoutCorrelation.Where(b => !pairedBolusIds.Contains(b.Id)))
            treatments.Add(ProjectCorrectionBolus(bolus));

        foreach (var carb in carbsWithoutCorrelation.Where(c => !pairedCarbIds.Contains(c.Id)))
            treatments.Add(ProjectCarbCorrection(carb, foodsByCarbId.GetValueOrDefault(carb.Id, [])));

        // --- BGCheck → Treatment ---
        foreach (var bgCheck in bgChecks)
            treatments.Add(ProjectBgCheck(bgCheck));

        // --- Note → Treatment ---
        foreach (var note in notes)
            treatments.Add(ProjectNote(note));

        // --- DeviceEvent → Treatment ---
        foreach (var deviceEvent in deviceEvents)
            treatments.Add(ProjectDeviceEvent(deviceEvent));

        return treatments.OrderByDescending(t => t.Mills).Take(limit);
    }

    // -------------------------------------------------------------------------
    // Private projection helpers
    // -------------------------------------------------------------------------

    private static Entry ProjectSensorGlucoseToEntry(SensorGlucose sg) =>
        new()
        {
            Id = sg.Id.ToString(),
            Type = "sgv",
            Mills = sg.Mills,
            Sgv = sg.Mgdl,
            Mgdl = sg.Mgdl,
            Mmol = sg.Mmol,
            Mbg = 0,
            Direction = sg.Direction?.ToString(),
            Trend = sg.Trend.HasValue ? (int?)sg.Trend.Value : null,
            TrendRate = sg.TrendRate,
            Noise = sg.Noise,
            Device = sg.Device,
            App = sg.App,
            DataSource = sg.DataSource,
        };

    private static Treatment ProjectMealBolus(Bolus bolus, CarbIntake carb, List<TreatmentFood> foods) =>
        new()
        {
            Id = bolus.Id.ToString(),
            EventType = TreatmentTypes.MealBolus,
            Mills = bolus.Mills,
            Insulin = bolus.Insulin,
            Carbs = carb.Carbs,
            FoodType = DeriveeFoodType(foods),
            Fat = DeriveTotalFat(foods),
            Protein = DeriveTotalProtein(foods),
            AbsorptionTime = carb.AbsorptionTime,
            CarbTime = carb.CarbTime.HasValue ? (int?)((int)carb.CarbTime.Value) : null,
            EnteredBy = bolus.Device,
            DataSource = bolus.DataSource,
            SyncIdentifier = bolus.SyncIdentifier,
            InsulinType = bolus.InsulinType,
        };

    private static Treatment ProjectCorrectionBolus(Bolus bolus) =>
        new()
        {
            Id = bolus.Id.ToString(),
            EventType = TreatmentTypes.CorrectionBolus,
            Mills = bolus.Mills,
            Insulin = bolus.Insulin,
            EnteredBy = bolus.Device,
            DataSource = bolus.DataSource,
            SyncIdentifier = bolus.SyncIdentifier,
            InsulinType = bolus.InsulinType,
        };

    private static Treatment ProjectCarbCorrection(CarbIntake carb, List<TreatmentFood> foods) =>
        new()
        {
            Id = carb.Id.ToString(),
            EventType = TreatmentTypes.CarbCorrection,
            Mills = carb.Mills,
            Carbs = carb.Carbs,
            FoodType = DeriveeFoodType(foods),
            Fat = DeriveTotalFat(foods),
            Protein = DeriveTotalProtein(foods),
            AbsorptionTime = carb.AbsorptionTime,
            CarbTime = carb.CarbTime.HasValue ? (int?)((int)carb.CarbTime.Value) : null,
            EnteredBy = carb.Device,
            DataSource = carb.DataSource,
            SyncIdentifier = carb.SyncIdentifier,
        };

    private static string? DeriveeFoodType(List<TreatmentFood> foods)
    {
        if (foods.Count == 0) return null;

        var names = foods
            .Select(f => f.FoodName ?? f.Note)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return names.Count > 0 ? string.Join(", ", names) : null;
    }

    private static double? DeriveTotalFat(List<TreatmentFood> foods)
    {
        var sum = foods
            .Where(f => f.FatPerPortion.HasValue && f.Portions > 0)
            .Sum(f => (double)(f.FatPerPortion!.Value * f.Portions));
        return sum > 0 ? sum : null;
    }

    private static double? DeriveTotalProtein(List<TreatmentFood> foods)
    {
        var sum = foods
            .Where(f => f.ProteinPerPortion.HasValue && f.Portions > 0)
            .Sum(f => (double)(f.ProteinPerPortion!.Value * f.Portions));
        return sum > 0 ? sum : null;
    }

    private static Treatment ProjectBgCheck(BGCheck bgCheck) =>
        new()
        {
            Id = bgCheck.Id.ToString(),
            EventType = TreatmentTypes.BgCheck,
            Mills = bgCheck.Mills,
            Glucose = bgCheck.Glucose,
            Mgdl = bgCheck.Mgdl,
            Mmol = bgCheck.Mmol,
            GlucoseType = bgCheck.GlucoseType?.ToString(),
            Units = bgCheck.Units == GlucoseUnit.Mmol ? "mmol" : "mg/dl",
            EnteredBy = bgCheck.Device,
            DataSource = bgCheck.DataSource,
            SyncIdentifier = bgCheck.SyncIdentifier,
        };

    private static Treatment ProjectNote(Note note) =>
        new()
        {
            Id = note.Id.ToString(),
            EventType = note.EventType ?? "Note",
            Mills = note.Mills,
            Notes = note.Text,
            IsAnnouncement = note.IsAnnouncement,
            EnteredBy = note.Device,
            DataSource = note.DataSource,
            SyncIdentifier = note.SyncIdentifier,
        };

    private static Treatment ProjectDeviceEvent(DeviceEvent deviceEvent)
    {
        DeviceEventTypeToString.TryGetValue(deviceEvent.EventType, out var eventTypeString);
        return new Treatment
        {
            Id = deviceEvent.Id.ToString(),
            EventType = eventTypeString ?? deviceEvent.EventType.ToString(),
            Mills = deviceEvent.Mills,
            Notes = deviceEvent.Notes,
            EnteredBy = deviceEvent.Device,
            DataSource = deviceEvent.DataSource,
            SyncIdentifier = deviceEvent.SyncIdentifier,
        };
    }

    private async Task<IEnumerable<T>> FetchSafe<T>(Func<Task<IEnumerable<T>>> fetchFunc)
    {
        try
        {
            return await fetchFunc();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch V4 records of type {Type} for legacy projection", typeof(T).Name);
            return Enumerable.Empty<T>();
        }
    }
}

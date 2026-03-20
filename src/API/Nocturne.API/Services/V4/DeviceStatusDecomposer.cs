using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Models;
using Nocturne.Core.Contracts.V4.Repositories;

using V4Models = Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.V4;

/// <summary>
/// Decomposes legacy DeviceStatus records into typed v4 snapshot tables.
/// Extracts APS (OpenAPS/AAPS/Trio and Loop), pump, and uploader snapshots
/// and persists them with idempotent create-or-update via LegacyId matching.
/// </summary>
public class DeviceStatusDecomposer : IDeviceStatusDecomposer, IDecomposer<DeviceStatus>
{
    private readonly IApsSnapshotRepository _apsRepo;
    private readonly IPumpSnapshotRepository _pumpRepo;
    private readonly IUploaderSnapshotRepository _uploaderRepo;
    private readonly IStateSpanService _stateSpanService;
    private readonly IDeviceService _deviceService;
    private readonly ILogger<DeviceStatusDecomposer> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public DeviceStatusDecomposer(
        IApsSnapshotRepository apsRepo,
        IPumpSnapshotRepository pumpRepo,
        IUploaderSnapshotRepository uploaderRepo,
        IStateSpanService stateSpanService,
        IDeviceService deviceService,
        ILogger<DeviceStatusDecomposer> logger)
    {
        _apsRepo = apsRepo;
        _pumpRepo = pumpRepo;
        _uploaderRepo = uploaderRepo;
        _stateSpanService = stateSpanService;
        _deviceService = deviceService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<V4Models.DecompositionResult> DecomposeAsync(DeviceStatus ds, CancellationToken ct = default)
    {
        var result = new V4Models.DecompositionResult
        {
            CorrelationId = Guid.CreateVersion7()
        };

        var legacyId = ds.Id;

        if (ds.OpenAps != null)
        {
            await DecomposeApsFromOpenApsAsync(ds, legacyId, result, ct);
        }
        else if (ds.Loop != null)
        {
            await DecomposeApsFromLoopAsync(ds, legacyId, result, ct);
        }

        if (ds.Pump != null)
        {
            await DecomposePumpAsync(ds, legacyId, result, ct);
        }

        if (ds.Uploader != null || ds.UploaderBattery.HasValue)
        {
            await DecomposeUploaderAsync(ds, legacyId, result, ct);
        }

        if (ds.Override is { Active: true })
        {
            await DecomposeOverrideAsync(ds, legacyId, result, ct);
        }

        return result;
    }

    #region APS Decomposition

    private async Task DecomposeApsFromOpenApsAsync(
        DeviceStatus ds, string? legacyId, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var command = ds.OpenAps!.Enacted ?? ds.OpenAps.Suggested;
        var predBGs = command?.PredBGs;
        var apsSystem = DetectOpenApsVariant(ds);

        var model = new V4Models.ApsSnapshot
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime,
            UtcOffset = ds.UtcOffset,
            Device = ds.Device,
            LegacyId = legacyId,
            AidAlgorithm = apsSystem,
            Iob = ds.OpenAps.Iob?.Iob,
            BasalIob = ds.OpenAps.Iob?.BasalIob,
            BolusIob = ds.OpenAps.Iob?.BolusIob,
            Cob = ds.OpenAps.Cob ?? command?.COB,
            CurrentBg = command?.Bg,
            EventualBg = command?.EventualBG,
            TargetBg = command?.TargetBG,
            RecommendedBolus = command?.InsulinReq,
            SensitivityRatio = command?.SensitivityRatio,
            Enacted = ds.OpenAps.Enacted != null
                && (ds.OpenAps.Enacted.Received == true || ds.OpenAps.Enacted.Recieved == true),
            EnactedRate = ds.OpenAps.Enacted?.Rate,
            EnactedDuration = ds.OpenAps.Enacted?.Duration,
            EnactedBolusVolume = ds.OpenAps.Enacted?.Smb,
            SuggestedJson = SerializeOrNull(ds.OpenAps.Suggested),
            EnactedJson = SerializeOrNull(ds.OpenAps.Enacted),
            // Trio uses oref multi-curve predictions exclusively; PredictedDefaultJson
            // is reserved for Loop's single combined curve. OpenAPS/AAPS duplicate IOB
            // into the default slot for backwards compatibility.
            PredictedDefaultJson = apsSystem == V4Models.AidAlgorithm.Trio
                ? null
                : SerializeOrNull(predBGs?.IOB),
            PredictedIobJson = SerializeOrNull(predBGs?.IOB),
            PredictedZtJson = SerializeOrNull(predBGs?.ZT),
            PredictedCobJson = SerializeOrNull(predBGs?.COB),
            PredictedUamJson = SerializeOrNull(predBGs?.UAM),
            PredictedStartTimestamp = ParseTimestampToDateTime(command?.Timestamp),
        };

        await UpsertApsSnapshotAsync(legacyId, model, result, ct);
    }

    private async Task DecomposeApsFromLoopAsync(
        DeviceStatus ds, string? legacyId, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var model = new V4Models.ApsSnapshot
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime,
            UtcOffset = ds.UtcOffset,
            Device = ds.Device,
            LegacyId = legacyId,
            AidAlgorithm = V4Models.AidAlgorithm.Loop,
            Iob = ds.Loop!.Iob?.Iob,
            BasalIob = ds.Loop.Iob?.BasalIob,
            BolusIob = null,
            Cob = ds.Loop.Cob?.Cob,
            CurrentBg = ds.Loop.Predicted?.Values?.FirstOrDefault(),
            EventualBg = ds.Loop.Predicted?.Values?.LastOrDefault(),
            RecommendedBolus = ds.Loop.RecommendedBolus,
            Enacted = ds.Loop.Enacted?.Received == true,
            EnactedRate = ds.Loop.Enacted?.Rate,
            EnactedDuration = ds.Loop.Enacted?.Duration,
            EnactedBolusVolume = ds.Loop.Enacted?.BolusVolume,
            SuggestedJson = SerializeOrNull(ds.Loop.Recommended),
            EnactedJson = SerializeOrNull(ds.Loop.Enacted),
            PredictedDefaultJson = SerializeOrNull(ds.Loop.Predicted?.Values),
            PredictedStartTimestamp = ParseTimestampToDateTime(ds.Loop.Predicted?.StartDate),
        };

        await UpsertApsSnapshotAsync(legacyId, model, result, ct);
    }

    private async Task UpsertApsSnapshotAsync(
        string? legacyId, V4Models.ApsSnapshot model, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var existing = legacyId != null
            ? await _apsRepo.GetByLegacyIdAsync(legacyId, ct)
            : null;

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _apsRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing ApsSnapshot {Id} from legacy device status {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _apsRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created ApsSnapshot from legacy device status {LegacyId}", legacyId);
        }
    }

    #endregion

    #region Pump Decomposition

    private async Task DecomposePumpAsync(
        DeviceStatus ds, string? legacyId, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var model = new V4Models.PumpSnapshot
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime,
            UtcOffset = ds.UtcOffset,
            Device = ds.Device,
            LegacyId = legacyId,
            Manufacturer = ds.Pump!.Manufacturer,
            Model = ds.Pump.Model,
            Reservoir = ds.Pump.Reservoir,
            ReservoirDisplay = ds.Pump.ReservoirDisplayOverride,
            BatteryPercent = ds.Pump.Battery?.Percent,
            BatteryVoltage = ds.Pump.Battery?.Voltage,
            Bolusing = ds.Pump.Status?.Bolusing,
            Suspended = ds.Pump.Status?.Suspended,
            PumpStatus = ds.Pump.Status?.Status,
            Clock = ds.Pump.Clock,
        };

        model.DeviceId = await _deviceService.ResolveAsync(
            V4Models.DeviceCategory.InsulinPump,
            ds.Pump?.Manufacturer,
            ds.Pump?.Model,
            ds.Mills, ct);

        var existing = legacyId != null
            ? await _pumpRepo.GetByLegacyIdAsync(legacyId, ct)
            : null;

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _pumpRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing PumpSnapshot {Id} from legacy device status {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _pumpRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created PumpSnapshot from legacy device status {LegacyId}", legacyId);
        }
    }

    #endregion

    #region Uploader Decomposition

    private async Task DecomposeUploaderAsync(
        DeviceStatus ds, string? legacyId, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var model = new V4Models.UploaderSnapshot
        {
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime,
            UtcOffset = ds.UtcOffset,
            Device = ds.Device,
            LegacyId = legacyId,
            Name = ds.Uploader?.Name,
            Battery = ds.Uploader?.Battery ?? ds.UploaderBattery,
            BatteryVoltage = ds.Uploader?.BatteryVoltage,
            IsCharging = ds.IsCharging,
            Temperature = ds.Uploader?.Temperature,
            Type = ds.Uploader?.Type,
        };

        model.DeviceId = await _deviceService.ResolveAsync(
            V4Models.DeviceCategory.Uploader,
            ds.Uploader?.Name,
            ds.Uploader?.Type ?? "unknown",
            ds.Mills, ct);

        var existing = legacyId != null
            ? await _uploaderRepo.GetByLegacyIdAsync(legacyId, ct)
            : null;

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _uploaderRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing UploaderSnapshot {Id} from legacy device status {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _uploaderRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created UploaderSnapshot from legacy device status {LegacyId}", legacyId);
        }
    }

    #endregion

    #region Override Decomposition

    private async Task DecomposeOverrideAsync(
        DeviceStatus ds, string? legacyId, V4Models.DecompositionResult result, CancellationToken ct)
    {
        var stateSpan = new StateSpan
        {
            Category = StateSpanCategory.Override,
            State = OverrideState.Custom.ToString(),
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills).UtcDateTime,
            EndTimestamp = ds.Override!.Duration is > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(ds.Mills + (long)(ds.Override.Duration.Value * 60000)).UtcDateTime
                : null,
            Source = ds.Device,
            OriginalId = legacyId,
            Metadata = BuildOverrideMetadata(ds.Override),
        };

        var upserted = await _stateSpanService.UpsertStateSpanAsync(stateSpan, ct);
        result.CreatedRecords.Add(upserted);
        _logger.LogDebug("Delegated Override from device status {LegacyId} to IStateSpanService", legacyId);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Distinguishes between vanilla OpenAPS, AAPS, and Trio based on payload heuristics.
    /// All three post under the "openaps" devicestatus key.
    /// - AAPS: uploader name contains "AndroidAPS"
    /// - Trio: openaps block includes a version field
    /// - Vanilla OpenAPS: neither of the above
    /// </summary>
    internal static V4Models.AidAlgorithm DetectOpenApsVariant(DeviceStatus ds)
    {
        if (ds.Uploader?.Name?.Contains("AndroidAPS", StringComparison.OrdinalIgnoreCase) == true)
            return V4Models.AidAlgorithm.AndroidAps;

        if (!string.IsNullOrEmpty(ds.OpenAps?.Version))
            return V4Models.AidAlgorithm.Trio;

        return V4Models.AidAlgorithm.OpenAps;
    }

    private static string? SerializeOrNull<T>(T? obj) where T : class
    {
        return obj is null ? null : JsonSerializer.Serialize(obj, JsonOptions);
    }

    private static string? SerializeOrNull(double[]? array)
    {
        return array is null ? null : JsonSerializer.Serialize(array, JsonOptions);
    }

    private static string? SerializeOrNull(List<double>? list)
    {
        return list is null ? null : JsonSerializer.Serialize(list, JsonOptions);
    }

    private static DateTime? ParseTimestampToDateTime(string? timestamp)
    {
        if (string.IsNullOrEmpty(timestamp))
            return null;
        return DateTimeOffset.TryParse(timestamp, out var dto) ? dto.UtcDateTime : null;
    }

    private static Dictionary<string, object>? BuildOverrideMetadata(OverrideStatus overrideStatus)
    {
        var metadata = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(overrideStatus.Name))
            metadata["name"] = overrideStatus.Name;

        if (overrideStatus.Multiplier.HasValue)
            metadata["multiplier"] = overrideStatus.Multiplier.Value;

        if (overrideStatus.CurrentCorrectionRange?.MinValue.HasValue == true)
            metadata["currentCorrectionRange.minValue"] = overrideStatus.CurrentCorrectionRange.MinValue.Value;

        if (overrideStatus.CurrentCorrectionRange?.MaxValue.HasValue == true)
            metadata["currentCorrectionRange.maxValue"] = overrideStatus.CurrentCorrectionRange.MaxValue.Value;

        return metadata.Count > 0 ? metadata : null;
    }

    #endregion

    /// <inheritdoc />
    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var deleted = 0;
        deleted += await _apsRepo.DeleteByLegacyIdAsync(legacyId, ct);
        deleted += await _pumpRepo.DeleteByLegacyIdAsync(legacyId, ct);
        deleted += await _uploaderRepo.DeleteByLegacyIdAsync(legacyId, ct);

        if (deleted > 0)
            _logger.LogDebug("Deleted {Count} v4 snapshot records for legacy device status {LegacyId}", deleted, legacyId);

        return deleted;
    }
}

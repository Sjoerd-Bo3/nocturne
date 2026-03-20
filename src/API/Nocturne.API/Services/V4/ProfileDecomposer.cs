using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;

using V4Models = Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.V4;

/// <summary>
/// Decomposes legacy Profile records into V4 granular models.
/// Iterates through the Profile.Store dictionary, producing one set of 5 V4 records per named profile.
/// Supports idempotent create-or-update via composite LegacyId matching ("{profileId}:{storeName}").
/// </summary>
public class ProfileDecomposer : IProfileDecomposer, IDecomposer<Profile>
{
    private readonly ITherapySettingsRepository _therapySettingsRepo;
    private readonly IBasalScheduleRepository _basalScheduleRepo;
    private readonly ICarbRatioScheduleRepository _carbRatioScheduleRepo;
    private readonly ISensitivityScheduleRepository _sensitivityScheduleRepo;
    private readonly ITargetRangeScheduleRepository _targetRangeScheduleRepo;
    private readonly ILogger<ProfileDecomposer> _logger;

    public ProfileDecomposer(
        ITherapySettingsRepository therapySettingsRepo,
        IBasalScheduleRepository basalScheduleRepo,
        ICarbRatioScheduleRepository carbRatioScheduleRepo,
        ISensitivityScheduleRepository sensitivityScheduleRepo,
        ITargetRangeScheduleRepository targetRangeScheduleRepo,
        ILogger<ProfileDecomposer> logger)
    {
        _therapySettingsRepo = therapySettingsRepo;
        _basalScheduleRepo = basalScheduleRepo;
        _carbRatioScheduleRepo = carbRatioScheduleRepo;
        _sensitivityScheduleRepo = sensitivityScheduleRepo;
        _targetRangeScheduleRepo = targetRangeScheduleRepo;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<V4Models.DecompositionResult> DecomposeAsync(Profile profile, CancellationToken ct = default)
    {
        var result = new V4Models.DecompositionResult
        {
            CorrelationId = Guid.CreateVersion7()
        };

        if (profile.Store.Count == 0)
        {
            _logger.LogWarning("Profile {Id} has no store entries, skipping decomposition", profile.Id);
            return result;
        }

        foreach (var (storeName, profileData) in profile.Store)
        {
            var legacyId = $"{profile.Id}:{storeName}";
            var isDefault = string.Equals(storeName, profile.DefaultProfile, StringComparison.OrdinalIgnoreCase);

            await DecomposeTherapySettingsAsync(profile, profileData, storeName, legacyId, isDefault, result, ct);
            await DecomposeBasalScheduleAsync(profile, profileData, storeName, legacyId, result, ct);
            await DecomposeCarbRatioScheduleAsync(profile, profileData, storeName, legacyId, result, ct);
            await DecomposeSensitivityScheduleAsync(profile, profileData, storeName, legacyId, result, ct);
            await DecomposeTargetRangeScheduleAsync(profile, profileData, storeName, legacyId, result, ct);
        }

        return result;
    }

    #region Decomposition Methods

    private async Task DecomposeTherapySettingsAsync(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        bool isDefault,
        V4Models.DecompositionResult result,
        CancellationToken ct)
    {
        var existing = await _therapySettingsRepo.GetByLegacyIdAsync(legacyId, ct);
        var model = MapToTherapySettings(profile, profileData, storeName, legacyId, isDefault, result.CorrelationId);

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _therapySettingsRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing TherapySettings {Id} from legacy profile {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _therapySettingsRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created TherapySettings from legacy profile {LegacyId}", legacyId);
        }
    }

    private async Task DecomposeBasalScheduleAsync(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        V4Models.DecompositionResult result,
        CancellationToken ct)
    {
        var existing = await _basalScheduleRepo.GetByLegacyIdAsync(legacyId, ct);
        var model = MapToBasalSchedule(profile, profileData, storeName, legacyId, result.CorrelationId);

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _basalScheduleRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing BasalSchedule {Id} from legacy profile {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _basalScheduleRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created BasalSchedule from legacy profile {LegacyId}", legacyId);
        }
    }

    private async Task DecomposeCarbRatioScheduleAsync(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        V4Models.DecompositionResult result,
        CancellationToken ct)
    {
        var existing = await _carbRatioScheduleRepo.GetByLegacyIdAsync(legacyId, ct);
        var model = MapToCarbRatioSchedule(profile, profileData, storeName, legacyId, result.CorrelationId);

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _carbRatioScheduleRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing CarbRatioSchedule {Id} from legacy profile {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _carbRatioScheduleRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created CarbRatioSchedule from legacy profile {LegacyId}", legacyId);
        }
    }

    private async Task DecomposeSensitivityScheduleAsync(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        V4Models.DecompositionResult result,
        CancellationToken ct)
    {
        var existing = await _sensitivityScheduleRepo.GetByLegacyIdAsync(legacyId, ct);
        var model = MapToSensitivitySchedule(profile, profileData, storeName, legacyId, result.CorrelationId);

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _sensitivityScheduleRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing SensitivitySchedule {Id} from legacy profile {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _sensitivityScheduleRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created SensitivitySchedule from legacy profile {LegacyId}", legacyId);
        }
    }

    private async Task DecomposeTargetRangeScheduleAsync(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        V4Models.DecompositionResult result,
        CancellationToken ct)
    {
        var existing = await _targetRangeScheduleRepo.GetByLegacyIdAsync(legacyId, ct);
        var model = MapToTargetRangeSchedule(profile, profileData, storeName, legacyId, result.CorrelationId);

        if (existing != null)
        {
            model.Id = existing.Id;
            var updated = await _targetRangeScheduleRepo.UpdateAsync(existing.Id, model, ct);
            result.UpdatedRecords.Add(updated);
            _logger.LogDebug("Updated existing TargetRangeSchedule {Id} from legacy profile {LegacyId}", existing.Id, legacyId);
        }
        else
        {
            var created = await _targetRangeScheduleRepo.CreateAsync(model, ct);
            result.CreatedRecords.Add(created);
            _logger.LogDebug("Created TargetRangeSchedule from legacy profile {LegacyId}", legacyId);
        }
    }

    #endregion

    #region Mapping Methods

    internal static V4Models.TherapySettings MapToTherapySettings(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        bool isDefault,
        Guid? correlationId)
    {
        return new V4Models.TherapySettings
        {
            LegacyId = legacyId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(profile.Mills).UtcDateTime,
            ProfileName = storeName,
            Timezone = profileData.Timezone,
            Units = profileData.Units ?? profile.Units,
            Dia = profileData.Dia,
            CarbsHr = profileData.CarbsHr,
            Delay = profileData.Delay,
            PerGIValues = profileData.PerGIValues,
            CarbsHrHigh = profileData.CarbsHrHigh,
            CarbsHrMedium = profileData.CarbsHrMedium,
            CarbsHrLow = profileData.CarbsHrLow,
            DelayHigh = profileData.DelayHigh,
            DelayMedium = profileData.DelayMedium,
            DelayLow = profileData.DelayLow,
            LoopSettings = profile.LoopSettings,
            IsDefault = isDefault,
            EnteredBy = profile.EnteredBy,
            IsExternallyManaged = profile.IsExternallyManaged,
            StartDate = profile.StartDate,
            Device = profile.EnteredBy,
            CorrelationId = correlationId,
        };
    }

    internal static V4Models.BasalSchedule MapToBasalSchedule(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        Guid? correlationId)
    {
        return new V4Models.BasalSchedule
        {
            LegacyId = legacyId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(profile.Mills).UtcDateTime,
            ProfileName = storeName,
            Entries = ConvertTimeValues(profileData.Basal),
            Device = profile.EnteredBy,
            CorrelationId = correlationId,
        };
    }

    internal static V4Models.CarbRatioSchedule MapToCarbRatioSchedule(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        Guid? correlationId)
    {
        return new V4Models.CarbRatioSchedule
        {
            LegacyId = legacyId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(profile.Mills).UtcDateTime,
            ProfileName = storeName,
            Entries = ConvertTimeValues(profileData.CarbRatio),
            Device = profile.EnteredBy,
            CorrelationId = correlationId,
        };
    }

    internal static V4Models.SensitivitySchedule MapToSensitivitySchedule(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        Guid? correlationId)
    {
        return new V4Models.SensitivitySchedule
        {
            LegacyId = legacyId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(profile.Mills).UtcDateTime,
            ProfileName = storeName,
            Entries = ConvertTimeValues(profileData.Sens),
            Device = profile.EnteredBy,
            CorrelationId = correlationId,
        };
    }

    internal static V4Models.TargetRangeSchedule MapToTargetRangeSchedule(
        Profile profile,
        ProfileData profileData,
        string storeName,
        string legacyId,
        Guid? correlationId)
    {
        return new V4Models.TargetRangeSchedule
        {
            LegacyId = legacyId,
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(profile.Mills).UtcDateTime,
            ProfileName = storeName,
            Entries = MergeTargets(profileData.TargetLow, profileData.TargetHigh),
            Device = profile.EnteredBy,
            CorrelationId = correlationId,
        };
    }

    #endregion

    #region Conversion Helpers

    internal static List<V4Models.ScheduleEntry> ConvertTimeValues(List<TimeValue> timeValues)
    {
        return timeValues.Select(tv =>
        {
            tv.EnsureTimeAsSeconds();
            return new V4Models.ScheduleEntry
            {
                Time = tv.Time,
                Value = tv.Value,
                TimeAsSeconds = tv.TimeAsSeconds,
            };
        }).ToList();
    }

    internal static List<V4Models.TargetRangeEntry> MergeTargets(List<TimeValue> lows, List<TimeValue> highs)
    {
        var highLookup = highs.ToDictionary(h => h.Time, h => h.Value);

        return lows.Select(low =>
        {
            low.EnsureTimeAsSeconds();
            return new V4Models.TargetRangeEntry
            {
                Time = low.Time,
                Low = low.Value,
                High = highLookup.TryGetValue(low.Time, out var high) ? high : low.Value,
                TimeAsSeconds = low.TimeAsSeconds,
            };
        }).ToList();
    }

    #endregion

    /// <inheritdoc />
    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var prefix = legacyId + ":";
        var deleted = 0;

        deleted += await _therapySettingsRepo.DeleteByLegacyIdPrefixAsync(prefix, ct);
        deleted += await _basalScheduleRepo.DeleteByLegacyIdPrefixAsync(prefix, ct);
        deleted += await _carbRatioScheduleRepo.DeleteByLegacyIdPrefixAsync(prefix, ct);
        deleted += await _sensitivityScheduleRepo.DeleteByLegacyIdPrefixAsync(prefix, ct);
        deleted += await _targetRangeScheduleRepo.DeleteByLegacyIdPrefixAsync(prefix, ct);

        if (deleted > 0)
            _logger.LogDebug("Deleted {Count} V4 records for legacy profile {LegacyId}", deleted, legacyId);

        return deleted;
    }
}

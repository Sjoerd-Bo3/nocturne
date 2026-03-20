using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.API.Services.V4;

/// <summary>
/// Decomposes legacy Activity records into HeartRate or StepCount records.
/// Detection is based on the presence of specific keys in Activity.AdditionalProperties.
/// </summary>
public class ActivityDecomposer : IActivityDecomposer, IDecomposer<Activity>
{
    private readonly NocturneDbContext _dbContext;
    private readonly ILogger<ActivityDecomposer> _logger;

    public ActivityDecomposer(NocturneDbContext dbContext, ILogger<ActivityDecomposer> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public bool IsHeartRate(Activity activity)
    {
        return activity.AdditionalProperties != null
            && activity.AdditionalProperties.ContainsKey("bpm");
    }

    public bool IsStepCount(Activity activity)
    {
        return activity.AdditionalProperties != null
            && activity.AdditionalProperties.ContainsKey("metric");
    }

    public bool IsSensorData(Activity activity)
    {
        return IsHeartRate(activity) || IsStepCount(activity);
    }

    public async Task<DecompositionResult> DecomposeAsync(
        Activity activity,
        CancellationToken ct = default
    )
    {
        var result = new DecompositionResult { CorrelationId = Guid.CreateVersion7() };

        if (IsHeartRate(activity))
        {
            await DecomposeHeartRateAsync(activity, result, ct);
        }
        else if (IsStepCount(activity))
        {
            await DecomposeStepCountAsync(activity, result, ct);
        }
        else
        {
            _logger.LogDebug(
                "Activity {Id} is a regular activity, skipping decomposition",
                activity.Id
            );
        }

        return result;
    }

    public async Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default)
    {
        var deleted = 0;

        var heartRateEntity = await _dbContext.HeartRates.FirstOrDefaultAsync(
            h => h.OriginalId == legacyId,
            ct
        );
        if (heartRateEntity != null)
        {
            _dbContext.HeartRates.Remove(heartRateEntity);
            deleted++;
        }

        var stepCountEntity = await _dbContext.StepCounts.FirstOrDefaultAsync(
            s => s.OriginalId == legacyId,
            ct
        );
        if (stepCountEntity != null)
        {
            _dbContext.StepCounts.Remove(stepCountEntity);
            deleted++;
        }

        if (deleted > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogDebug(
                "Deleted {Count} decomposed records for legacy activity {LegacyId}",
                deleted,
                legacyId
            );
        }

        return deleted;
    }

    // --- Reverse mapping for backward-compat GET ---

    internal static Activity HeartRateToActivity(HeartRate heartRate)
    {
        var activity = new Activity
        {
            Id = heartRate.Id,
            Mills = heartRate.Mills,
            CreatedAt = heartRate.CreatedAt,
            UtcOffset = heartRate.UtcOffset,
            EnteredBy = heartRate.EnteredBy,
            AdditionalProperties = new Dictionary<string, object>
            {
                ["bpm"] = heartRate.Bpm,
                ["accuracy"] = heartRate.Accuracy,
            },
        };

        if (heartRate.Device != null)
            activity.AdditionalProperties["device"] = heartRate.Device;

        return activity;
    }

    internal static Activity StepCountToActivity(StepCount stepCount)
    {
        var activity = new Activity
        {
            Id = stepCount.Id,
            Mills = stepCount.Mills,
            CreatedAt = stepCount.CreatedAt,
            UtcOffset = stepCount.UtcOffset,
            EnteredBy = stepCount.EnteredBy,
            AdditionalProperties = new Dictionary<string, object>
            {
                ["metric"] = stepCount.Metric,
                ["source"] = stepCount.Source,
            },
        };

        if (stepCount.Device != null)
            activity.AdditionalProperties["device"] = stepCount.Device;

        return activity;
    }

    // --- Private decomposition methods ---

    private async Task DecomposeHeartRateAsync(
        Activity activity,
        DecompositionResult result,
        CancellationToken ct
    )
    {
        var existing =
            activity.Id != null
                ? await _dbContext.HeartRates.FirstOrDefaultAsync(
                    h => h.OriginalId == activity.Id,
                    ct
                )
                : null;

        var heartRate = MapToHeartRate(activity);

        if (existing != null)
        {
            HeartRateMapper.UpdateEntity(existing, heartRate);
            await _dbContext.SaveChangesAsync(ct);
            result.UpdatedRecords.Add(HeartRateMapper.ToDomainModel(existing));
            _logger.LogDebug(
                "Updated existing HeartRate {Id} from legacy activity {LegacyId}",
                existing.Id,
                activity.Id
            );
        }
        else
        {
            var entity = HeartRateMapper.ToEntity(heartRate);
            await _dbContext.HeartRates.AddAsync(entity, ct);
            await _dbContext.SaveChangesAsync(ct);
            result.CreatedRecords.Add(HeartRateMapper.ToDomainModel(entity));
            _logger.LogDebug("Created HeartRate from legacy activity {LegacyId}", activity.Id);
        }
    }

    private async Task DecomposeStepCountAsync(
        Activity activity,
        DecompositionResult result,
        CancellationToken ct
    )
    {
        var existing =
            activity.Id != null
                ? await _dbContext.StepCounts.FirstOrDefaultAsync(
                    s => s.OriginalId == activity.Id,
                    ct
                )
                : null;

        var stepCount = MapToStepCount(activity);

        if (existing != null)
        {
            StepCountMapper.UpdateEntity(existing, stepCount);
            await _dbContext.SaveChangesAsync(ct);
            result.UpdatedRecords.Add(StepCountMapper.ToDomainModel(existing));
            _logger.LogDebug(
                "Updated existing StepCount {Id} from legacy activity {LegacyId}",
                existing.Id,
                activity.Id
            );
        }
        else
        {
            var entity = StepCountMapper.ToEntity(stepCount);
            await _dbContext.StepCounts.AddAsync(entity, ct);
            await _dbContext.SaveChangesAsync(ct);
            result.CreatedRecords.Add(StepCountMapper.ToDomainModel(entity));
            _logger.LogDebug("Created StepCount from legacy activity {LegacyId}", activity.Id);
        }
    }

    // --- Mapping helpers ---

    internal static HeartRate MapToHeartRate(Activity activity)
    {
        var props = activity.AdditionalProperties ?? new Dictionary<string, object>();

        return new HeartRate
        {
            Id = activity.Id,
            Mills = activity.Mills,
            Bpm = GetIntValue(props, "bpm"),
            Accuracy = GetIntValue(props, "accuracy"),
            Device = GetStringValue(props, "device") ?? activity.EnteredBy,
            EnteredBy = activity.EnteredBy,
            CreatedAt = activity.CreatedAt,
            UtcOffset = activity.UtcOffset,
        };
    }

    internal static StepCount MapToStepCount(Activity activity)
    {
        var props = activity.AdditionalProperties ?? new Dictionary<string, object>();

        return new StepCount
        {
            Id = activity.Id,
            Mills = activity.Mills,
            Metric = GetIntValue(props, "metric"),
            Source = GetIntValue(props, "source"),
            Device = GetStringValue(props, "device") ?? activity.EnteredBy,
            EnteredBy = activity.EnteredBy,
            CreatedAt = activity.CreatedAt,
            UtcOffset = activity.UtcOffset,
        };
    }

    private static int GetIntValue(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return 0;

        return value switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            System.Text.Json.JsonElement je
                when je.ValueKind == System.Text.Json.JsonValueKind.Number
                => je.GetInt32(),
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => 0,
        };
    }

    private static string? GetStringValue(Dictionary<string, object> props, string key)
    {
        if (!props.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            string s => s,
            System.Text.Json.JsonElement je
                when je.ValueKind == System.Text.Json.JsonValueKind.String
                => je.GetString(),
            _ => value?.ToString(),
        };
    }
}

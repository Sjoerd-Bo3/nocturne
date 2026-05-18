using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Monitoring;

/// <summary>
/// Represents an alert fired when a tracker crosses a configured threshold.
/// </summary>
public record TrackerAlert(
    Guid InstanceId,
    Guid DefinitionId,
    Guid ThresholdId,
    string TrackerName,
    NotificationUrgency Urgency,
    string Message,
    TrackerAlertConfig Config
);

/// <summary>
/// Notification delivery configuration for a tracker alert threshold (push, audio, vibration, repeat settings).
/// </summary>
public record TrackerAlertConfig(
    bool PushEnabled,
    bool AudioEnabled,
    string? AudioSound,
    bool VibrateEnabled,
    int RepeatIntervalMins,
    int MaxRepeats,
    bool RespectQuietHours
);

/// <summary>
/// Service to evaluate tracker instances against thresholds and generate alerts
/// </summary>
public interface ITrackerAlertService
{
    /// <summary>
    /// Evaluate all active tracker instances and generate pending alerts
    /// </summary>
    Task<List<TrackerAlert>> EvaluateActiveTrackersAsync(string userId, CancellationToken ct = default);

    /// <summary>
    /// Get pending (not yet displayed/sent) tracker alerts for a user
    /// </summary>
    Task<List<TrackerAlert>> GetPendingAlertsAsync(string userId, CancellationToken ct = default);
}

public class TrackerAlertService : ITrackerAlertService
{
    private readonly ITrackerRepository _repository;
    private readonly ILogger<TrackerAlertService> _logger;

    public TrackerAlertService(
        ITrackerRepository repository,
        ILogger<TrackerAlertService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<TrackerAlert>> EvaluateActiveTrackersAsync(string userId, CancellationToken ct = default)
    {
        var alerts = new List<TrackerAlert>();

        // Get all active tracker instances for the user
        var instances = await _repository.GetActiveInstancesAsync(userId, ct);

        foreach (var instance in instances)
        {
            var definition = instance.Definition;
            if (definition == null)
            {
                _logger.LogWarning("Tracker instance {InstanceId} has no definition", instance.Id);
                continue;
            }

            // Check each notification threshold
            foreach (var threshold in definition.NotificationThresholds.OrderBy(t => t.DisplayOrder))
            {
                var alert = EvaluateThreshold(instance, definition, threshold);
                if (alert != null)
                {
                    alerts.Add(alert);
                }
            }
        }

        return alerts;
    }

    /// <inheritdoc />
    public async Task<List<TrackerAlert>> GetPendingAlertsAsync(string userId, CancellationToken ct = default)
    {
        // Evaluate all active trackers
        // TODO: Re-add quiet hours filtering when new alert engine is implemented
        return await EvaluateActiveTrackersAsync(userId, ct);
    }

    /// <summary>
    /// Evaluate a single threshold against an instance
    /// </summary>
    private TrackerAlert? EvaluateThreshold(
        TrackerInstanceEntity instance,
        TrackerDefinitionEntity definition,
        TrackerNotificationThresholdEntity threshold)
    {
        // Calculate effective hours based on mode
        double hoursFromReference;
        double effectiveThresholdHours;

        if (definition.Mode == TrackerMode.Event)
        {
            // Event mode: hours relative to ScheduledAt
            // Negative threshold = before event, Positive = after event
            if (!instance.ScheduledAt.HasValue)
            {
                _logger.LogWarning(
                    "Event mode tracker instance {InstanceId} has no ScheduledAt",
                    instance.Id);
                return null;
            }

            hoursFromReference = (DateTime.UtcNow - instance.ScheduledAt.Value).TotalHours;
            effectiveThresholdHours = threshold.Hours;
        }
        else
        {
            // Duration mode: hours relative to StartedAt
            // Negative thresholds are relative to lifespan end
            hoursFromReference = instance.AgeHours;

            if (threshold.Hours >= 0)
            {
                effectiveThresholdHours = threshold.Hours;
            }
            else
            {
                // Negative threshold: trigger X hours before lifespan ends
                if (!definition.LifespanHours.HasValue)
                {
                    _logger.LogWarning(
                        "Negative threshold on tracker {DefinitionId} without lifespan",
                        definition.Id);
                    return null;
                }
                effectiveThresholdHours = definition.LifespanHours.Value + threshold.Hours;
            }
        }

        // Check if threshold is crossed
        if (hoursFromReference < effectiveThresholdHours)
        {
            return null; // Not yet at threshold
        }

        // Check if snoozed
        if (IsSnoozed(instance))
        {
            _logger.LogDebug(
                "Tracker instance {InstanceId} is snoozed until {SnoozeEnd}",
                instance.Id,
                instance.LastAckedAt!.Value.AddMinutes(instance.AckSnoozeMins ?? 0));
            return null;
        }

        // Check if any alert features are enabled
        if (!threshold.PushEnabled && !threshold.AudioEnabled && !threshold.VibrateEnabled)
        {
            return null; // No alert actions configured
        }

        // Generate the alert message
        var message = threshold.Description
            ?? GenerateDefaultMessage(definition, threshold, instance);

        return new TrackerAlert(
            InstanceId: instance.Id,
            DefinitionId: definition.Id,
            ThresholdId: threshold.Id,
            TrackerName: definition.Name,
            Urgency: threshold.Urgency,
            Message: message,
            Config: new TrackerAlertConfig(
                PushEnabled: threshold.PushEnabled,
                AudioEnabled: threshold.AudioEnabled,
                AudioSound: threshold.AudioSound,
                VibrateEnabled: threshold.VibrateEnabled,
                RepeatIntervalMins: threshold.RepeatIntervalMins,
                MaxRepeats: threshold.MaxRepeats,
                RespectQuietHours: threshold.RespectQuietHours
            )
        );
    }

    /// <summary>
    /// Generate default alert message based on mode and threshold
    /// </summary>
    private static string GenerateDefaultMessage(
        TrackerDefinitionEntity definition,
        TrackerNotificationThresholdEntity threshold,
        TrackerInstanceEntity instance)
    {
        if (definition.Mode == TrackerMode.Event)
        {
            if (threshold.Hours < 0)
            {
                return $"{definition.Name} in {Math.Abs(threshold.Hours)} hours";
            }
            return $"{definition.Name} was {threshold.Hours} hours ago";
        }

        return $"{definition.Name} has been active for {threshold.Hours} hours";
    }

    /// <summary>
    /// Check if an instance is currently snoozed
    /// </summary>
    private static bool IsSnoozed(TrackerInstanceEntity instance)
    {
        if (!instance.LastAckedAt.HasValue || !instance.AckSnoozeMins.HasValue)
        {
            return false;
        }

        var snoozeEnd = instance.LastAckedAt.Value.AddMinutes(instance.AckSnoozeMins.Value);
        return DateTime.UtcNow < snoozeEnd;
    }

}

using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Abstractions;

/// <summary>
/// Repository port for Tracker operations (definitions, instances, presets)
/// </summary>
public interface ITrackerRepository
{
    // Definitions

    Task<List<TrackerDefinitionEntity>> GetDefinitionsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<List<TrackerDefinitionEntity>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default);

    Task<List<TrackerDefinitionEntity>> GetDefinitionsByCategoryAsync(
        string userId,
        TrackerCategory category,
        CancellationToken cancellationToken = default);

    Task<TrackerDefinitionEntity[]> GetFavoriteDefinitionsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<TrackerDefinitionEntity?> GetDefinitionByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TrackerDefinitionEntity> CreateDefinitionAsync(
        TrackerDefinitionEntity definition,
        CancellationToken cancellationToken = default);

    Task<TrackerDefinitionEntity?> UpdateDefinitionAsync(
        Guid id,
        TrackerDefinitionEntity updated,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteDefinitionAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task UpdateNotificationThresholdsAsync(
        Guid definitionId,
        List<TrackerNotificationThresholdEntity> thresholds,
        CancellationToken cancellationToken = default);

    // Instances

    Task<TrackerInstanceEntity[]> GetActiveInstancesAsync(
        string? userId,
        CancellationToken cancellationToken = default);

    Task<List<TrackerInstanceEntity>> GetActiveInstancesForDefinitionAsync(
        Guid definitionId,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity[]> GetCompletedInstancesAsync(
        string userId,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity[]> GetUpcomingInstancesAsync(
        string? userId,
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity?> GetInstanceByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity> StartInstanceAsync(
        Guid definitionId,
        string userId,
        string? startNotes = null,
        string? startTreatmentId = null,
        DateTime? startedAt = null,
        DateTime? scheduledAt = null,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity?> CompleteInstanceAsync(
        Guid instanceId,
        CompletionReason reason,
        string? completionNotes = null,
        string? completeTreatmentId = null,
        DateTime? completedAt = null,
        CancellationToken cancellationToken = default);

    Task<bool> AckInstanceAsync(
        Guid instanceId,
        int snoozeMins,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteInstanceAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // Presets

    Task<TrackerPresetEntity[]> GetPresetsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<TrackerPresetEntity?> GetPresetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<TrackerPresetEntity> CreatePresetAsync(
        TrackerPresetEntity preset,
        CancellationToken cancellationToken = default);

    Task<TrackerInstanceEntity?> ApplyPresetAsync(
        Guid presetId,
        string userId,
        string? overrideNotes = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePresetAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Abstractions;

/// <summary>
/// Repository port for in-app notification operations
/// </summary>
public interface IInAppNotificationRepository
{
    /// <summary>
    /// Gets all active notifications for a specific user
    /// </summary>
    Task<List<InAppNotificationEntity>> GetActiveAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a notification by its unique identifier
    /// </summary>
    Task<InAppNotificationEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all notifications that are pending resolution
    /// </summary>
    Task<List<InAppNotificationEntity>> GetPendingResolutionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new in-app notification
    /// </summary>
    Task<InAppNotificationEntity> CreateAsync(
        InAppNotificationEntity entity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives a notification with a specified reason
    /// </summary>
    Task<InAppNotificationEntity?> ArchiveAsync(
        Guid id,
        NotificationArchiveReason reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a notification by its source identifier and type for a user
    /// </summary>
    Task<InAppNotificationEntity?> FindBySourceAsync(
        string userId,
        InAppNotificationType type,
        string sourceId,
        CancellationToken cancellationToken = default);
}

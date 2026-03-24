using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Abstractions;

/// <summary>
/// Repository port for in-app notification operations
/// </summary>
public interface IInAppNotificationRepository
{
    Task<List<InAppNotificationEntity>> GetActiveAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<InAppNotificationEntity?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<InAppNotificationEntity>> GetPendingResolutionAsync(
        CancellationToken cancellationToken = default);

    Task<InAppNotificationEntity> CreateAsync(
        InAppNotificationEntity entity,
        CancellationToken cancellationToken = default);

    Task<InAppNotificationEntity?> ArchiveAsync(
        Guid id,
        NotificationArchiveReason reason,
        CancellationToken cancellationToken = default);

    Task<InAppNotificationEntity?> FindBySourceAsync(
        string userId,
        InAppNotificationType type,
        string sourceId,
        CancellationToken cancellationToken = default);
}

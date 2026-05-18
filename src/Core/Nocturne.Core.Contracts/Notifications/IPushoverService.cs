using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Notifications;

/// <summary>
/// Interface for Pushover notification service
/// Provides functionality for sending notifications to Pushover API with 1:1 legacy compatibility
/// </summary>
public interface IPushoverService
{
    /// <summary>
    /// Sends a Pushover notification
    /// Maintains 1:1 compatibility with legacy Pushover notification sending
    /// </summary>
    /// <param name="request">Pushover notification request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pushover response with receipt information</returns>
    Task<PushoverResponse> SendNotificationAsync(
        PushoverNotificationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a Pushover notification request from alarm details
    /// Implements legacy alarm-to-Pushover mapping logic
    /// </summary>
    /// <param name="level">Alarm level (1=WARN, 2=URGENT)</param>
    /// <param name="group">Alarm group</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="sound">Pushover sound (optional)</param>
    /// <returns>Pushover notification request</returns>
    PushoverNotificationRequest CreateAlarmNotification(
        int level,
        string group,
        string title,
        string message,
        string? sound = null
    );
}

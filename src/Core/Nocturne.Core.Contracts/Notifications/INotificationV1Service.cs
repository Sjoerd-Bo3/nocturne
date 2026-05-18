using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Notifications;

/// <summary>
/// Service interface for V1 notification operations with 1:1 legacy JavaScript compatibility
/// Handles notification acknowledgments, Pushover callbacks, and admin notifications
/// Based on the legacy notifications.js and adminnotifies.js implementations
/// </summary>
public interface INotificationV1Service
{
    /// <summary>
    /// Acknowledges a notification alarm to silence it
    /// Implements the legacy notifications.ack() functionality
    /// </summary>
    /// <param name="request">Acknowledgment request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Acknowledgment response</returns>
    Task<NotificationAckResponse> AckNotificationAsync(
        NotificationAckRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Handles Pushover webhook callbacks for notification acknowledgments
    /// Processes callbacks from Pushover service when notifications are acknowledged
    /// </summary>
    /// <param name="request">Pushover callback data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    Task<NotificationAckResponse> ProcessPushoverCallbackAsync(
        PushoverCallbackRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets all admin notifications with their counts and timestamps
    /// Implements the legacy adminnotifies.getNotifies() functionality with admin permission check
    /// </summary>
    /// <param name="subjectId">Subject ID for authorization check (implements legacy ctx.authorization.resolveWithRequest)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Admin notifications response</returns>
    Task<AdminNotifiesResponse> GetAdminNotifiesAsync(
        string? subjectId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Adds a new admin notification or increments count if it already exists
    /// Implements the legacy adminnotifies.addNotify() functionality
    /// </summary>
    /// <param name="notification">Admin notification to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success response</returns>
    Task<NotificationAckResponse> AddAdminNotificationAsync(
        AdminNotification notification,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cleans up old admin notifications (older than 12 hours unless persistent)
    /// Implements the legacy adminnotifies.clean() functionality
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the cleanup operation</returns>
    Task CleanAdminNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all admin notifications
    /// Implements the legacy adminnotifies.cleanAll() functionality
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the cleanup operation</returns>
    Task ClearAllAdminNotificationsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a Pushover receipt for an alarm to enable proper acknowledgment mapping
    /// This method should be called when a Pushover notification is sent for an alarm
    /// </summary>
    /// <param name="receipt">Pushover receipt ID</param>
    /// <param name="level">Alarm level</param>
    /// <param name="group">Alarm group</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    void RegisterPushoverReceipt(
        string receipt,
        int level,
        string group,
        string title,
        string message
    );

    /// <summary>
    /// Cleans up expired Pushover receipts (older than 24 hours)
    /// Pushover receipts expire after 24 hours, so we can safely remove them
    /// </summary>
    void CleanExpiredPushoverReceipts();

    /// <summary>
    /// Gets the count of active Pushover receipts for monitoring/debugging
    /// </summary>
    /// <returns>Number of active Pushover receipts</returns>
    int GetActivePushoverReceiptCount();

    /// <summary>
    /// Sends a Pushover notification for an alarm
    /// Implements legacy Pushover notification sending with full 1:1 compatibility
    /// </summary>
    /// <param name="level">Alarm level (1=WARN, 2=URGENT)</param>
    /// <param name="group">Alarm group</param>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="sound">Pushover sound (optional)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Response indicating success or failure</returns>
    Task<NotificationAckResponse> SendPushoverNotificationAsync(
        int level,
        string group,
        string title,
        string message,
        string? sound = null,
        CancellationToken cancellationToken = default
    );
}

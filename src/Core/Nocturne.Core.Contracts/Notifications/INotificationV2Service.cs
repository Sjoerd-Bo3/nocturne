using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Notifications;

/// <summary>
/// Service interface for V2 notification operations with 1:1 legacy JavaScript compatibility
/// Handles Loop notifications and enhanced notification system features
/// </summary>
public interface INotificationV2Service
{
    /// <summary>
    /// Sends a Loop notification for iOS Loop app integration
    /// </summary>
    /// <param name="request">Loop notification request data</param>
    /// <param name="remoteAddress">IP address of the requesting client</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response indicating success or failure</returns>
    Task<NotificationV2Response> SendLoopNotificationAsync(
        LoopNotificationRequest request,
        string remoteAddress,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Processes a generic V2 notification
    /// </summary>
    /// <param name="notification">Base notification data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Notification response indicating success or failure</returns>
    Task<NotificationV2Response> ProcessNotificationAsync(
        NotificationBase notification,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the current notification status and configuration
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current notification system status</returns>
    Task<object> GetNotificationStatusAsync(CancellationToken cancellationToken = default);
}

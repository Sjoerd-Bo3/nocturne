namespace Nocturne.Core.Contracts.Auth;

/// <summary>
/// Service for recording authentication and authorization audit events.
/// </summary>
public interface IAuthAuditService
{
    /// <summary>
    /// Log an authentication or authorization event.
    /// </summary>
    /// <param name="eventType">One of the <c>AuthAuditEventType</c> constants.</param>
    /// <param name="subjectId">The subject involved, if known.</param>
    /// <param name="success">Whether the event succeeded.</param>
    /// <param name="ipAddress">Client IP address.</param>
    /// <param name="userAgent">Client user-agent string.</param>
    /// <param name="errorMessage">Error message on failure.</param>
    /// <param name="detailsJson">Additional details as a JSON string (stored as jsonb).</param>
    /// <param name="refreshTokenId">Related refresh token, if applicable.</param>
    Task LogAsync(string eventType, Guid? subjectId, bool success,
        string? ipAddress = null, string? userAgent = null,
        string? errorMessage = null, string? detailsJson = null,
        Guid? refreshTokenId = null);
}

namespace Nocturne.Core.Contracts.Auth;

/// <summary>
/// Service for managing refresh tokens (stored in database)
/// </summary>
public interface IRefreshTokenService
{
    /// <summary>
    /// Create and store a new refresh token for a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="oidcSessionId">OIDC session ID (for logout)</param>
    /// <param name="deviceDescription">Device description for session management</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">User agent string</param>
    /// <returns>Plain refresh token string (only returned once)</returns>
    Task<string> CreateRefreshTokenAsync(
        Guid subjectId,
        string? oidcSessionId = null,
        string? deviceDescription = null,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Validate a refresh token and return subject ID if valid
    /// </summary>
    /// <param name="refreshToken">Plain refresh token string</param>
    /// <returns>Subject ID if valid, null if invalid/expired/revoked</returns>
    Task<Guid?> ValidateRefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Rotate a refresh token - invalidate old one and create new one
    /// Used for refresh token rotation security pattern
    /// </summary>
    /// <param name="oldRefreshToken">Current refresh token to rotate</param>
    /// <param name="ipAddress">Client IP address</param>
    /// <param name="userAgent">User agent string</param>
    /// <returns>New refresh token if rotation succeeded, null if old token invalid</returns>
    Task<string?> RotateRefreshTokenAsync(
        string oldRefreshToken,
        string? ipAddress = null,
        string? userAgent = null);

    /// <summary>
    /// Revoke a specific refresh token
    /// </summary>
    /// <param name="refreshToken">Plain refresh token string</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>True if revoked, false if not found</returns>
    Task<bool> RevokeRefreshTokenAsync(string refreshToken, string reason);

    /// <summary>
    /// Revoke all refresh tokens for a subject (logout from all devices)
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeAllRefreshTokensForSubjectAsync(Guid subjectId, string reason);

    /// <summary>
    /// Revoke all refresh tokens for an OIDC session
    /// </summary>
    /// <param name="oidcSessionId">OIDC session identifier</param>
    /// <param name="reason">Reason for revocation</param>
    /// <returns>Number of tokens revoked</returns>
    Task<int> RevokeRefreshTokensByOidcSessionAsync(string oidcSessionId, string reason);

    /// <summary>
    /// Get all active refresh tokens for a subject (for session management UI)
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>List of active sessions</returns>
    Task<List<RefreshTokenInfo>> GetActiveSessionsForSubjectAsync(Guid subjectId);

    /// <summary>
    /// Update last used timestamp for a refresh token
    /// </summary>
    /// <param name="refreshToken">Plain refresh token string</param>
    Task UpdateLastUsedAsync(string refreshToken);

    /// <summary>
    /// Delete expired refresh tokens from the database
    /// </summary>
    /// <param name="olderThan">Delete tokens that expired before this date</param>
    /// <returns>Number of tokens deleted</returns>
    Task<int> PruneExpiredRefreshTokensAsync(DateTime? olderThan = null);
}

/// <summary>
/// Refresh token information for session management
/// </summary>
public class RefreshTokenInfo
{
    /// <summary>
    /// Token ID (not the token itself)
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Device description
    /// </summary>
    public string? DeviceDescription { get; set; }

    /// <summary>
    /// IP address when created
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// When the token was issued
    /// </summary>
    public DateTime IssuedAt { get; set; }

    /// <summary>
    /// When the token was last used
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// When the token expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Whether this is the current session
    /// </summary>
    public bool IsCurrent { get; set; }
}

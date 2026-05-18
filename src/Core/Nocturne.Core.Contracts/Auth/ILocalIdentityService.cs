namespace Nocturne.Core.Contracts.Auth;

/// <summary>
/// Service for the built-in local identity provider
/// Handles user registration, authentication, and password management
/// </summary>
public interface ILocalIdentityService
{
    /// <summary>
    /// Check if a user with this email exists
    /// </summary>
    /// <param name="email">Email address</param>
    /// <returns>True if user exists</returns>
    Task<bool> UserExistsAsync(string email);

    /// <summary>
    /// Register a new local user
    /// </summary>
    /// <param name="request">Registration request</param>
    /// <returns>Registration result</returns>
    Task<LocalRegistrationResult> RegisterAsync(LocalRegistrationRequest request);

    /// <summary>
    /// Register a new local user with additional options (used for admin seeding)
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="password">Password</param>
    /// <param name="displayName">Display name (optional)</param>
    /// <param name="skipAllowlistCheck">Skip allowlist validation</param>
    /// <param name="autoVerifyEmail">Automatically verify email</param>
    /// <returns>Registration result</returns>
    Task<LocalRegistrationResult> RegisterAsync(
        string email,
        string password,
        string? displayName = null,
        bool skipAllowlistCheck = false,
        bool autoVerifyEmail = false
    );

    /// <summary>
    /// Authenticate a user with email and password
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="password">Password</param>
    /// <param name="ipAddress">IP address of the client (for rate limiting)</param>
    /// <returns>Authentication result</returns>
    Task<LocalAuthResult> AuthenticateAsync(
        string email,
        string password,
        string? ipAddress = null
    );

    /// <summary>
    /// Verify an email address using the verification token
    /// </summary>
    /// <param name="token">Verification token</param>
    /// <returns>True if verification succeeded</returns>
    Task<bool> VerifyEmailAsync(string token);

    /// <summary>
    /// Request a password reset
    /// </summary>
    /// <param name="email">Email address</param>
    /// <param name="ipAddress">IP address of the requester</param>
    /// <param name="userAgent">User agent of the requester</param>
    /// <returns>Result indicating whether reset was initiated</returns>
    Task<PasswordResetRequestResult> RequestPasswordResetAsync(
        string email,
        string? ipAddress = null,
        string? userAgent = null
    );

    /// <summary>
    /// Reset password using a reset token
    /// </summary>
    /// <param name="token">Reset token</param>
    /// <param name="newPassword">New password</param>
    /// <returns>True if reset succeeded</returns>
    Task<bool> ResetPasswordAsync(string token, string newPassword);

    /// <summary>
    /// Change password for an authenticated user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="currentPassword">Current password</param>
    /// <param name="newPassword">New password</param>
    /// <returns>True if change succeeded</returns>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword);

    /// <summary>
    /// Get a local user by ID
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>User if found</returns>
    Task<LocalUser?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// Get a local user by email
    /// </summary>
    /// <param name="email">Email address</param>
    /// <returns>User if found</returns>
    Task<LocalUser?> GetUserByEmailAsync(string email);

    /// <summary>
    /// Get all local users (admin function)
    /// </summary>
    /// <param name="filter">Optional filter</param>
    /// <returns>List of users</returns>
    Task<List<LocalUser>> GetUsersAsync(LocalUserFilter? filter = null);

    /// <summary>
    /// Approve a pending user registration (admin function)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="adminId">Admin performing the approval</param>
    /// <returns>True if approved</returns>
    Task<bool> ApproveUserAsync(Guid userId, Guid adminId);

    /// <summary>
    /// Reject a pending user registration (admin function)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="adminId">Admin performing the rejection</param>
    /// <param name="reason">Reason for rejection</param>
    /// <returns>True if rejected</returns>
    Task<bool> RejectUserAsync(Guid userId, Guid adminId, string? reason = null);

    /// <summary>
    /// Get pending password reset requests (admin function)
    /// </summary>
    /// <returns>List of pending requests</returns>
    Task<List<PasswordResetRequest>> GetPendingPasswordResetRequestsAsync();

    /// <summary>
    /// Handle a password reset request manually (admin function)
    /// Generates a reset token for the admin to provide to the user
    /// </summary>
    /// <param name="requestId">Password reset request ID</param>
    /// <param name="adminId">Admin handling the request</param>
    /// <returns>Reset URL for admin to share with user</returns>
    Task<string?> HandlePasswordResetRequestAsync(Guid requestId, Guid adminId);

    /// <summary>
    /// Set a temporary password for a user (admin function)
    /// Sets the RequirePasswordChange flag to true
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="temporaryPassword">Temporary password (can be empty string)</param>
    /// <param name="adminId">Admin performing the action</param>
    /// <returns>True if successful</returns>
    Task<bool> SetTemporaryPasswordAsync(Guid userId, string temporaryPassword, Guid adminId);

    /// <summary>
    /// Clear the require password change flag after user changes password
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>True if successful</returns>
    Task<bool> ClearRequirePasswordChangeAsync(Guid userId);

    /// <summary>
    /// Check if an email is allowed to register based on allowlist
    /// </summary>
    /// <param name="email">Email to check</param>
    /// <returns>Allowlist check result</returns>
    AllowlistCheckResult CheckAllowlist(string email);

    /// <summary>
    /// Validate a password against requirements
    /// </summary>
    /// <param name="password">Password to validate</param>
    /// <returns>Validation result with any errors</returns>
    PasswordValidationResult ValidatePassword(string password);
}

/// <summary>
/// Request to register a new local user
/// </summary>
public class LocalRegistrationRequest
{
    /// <summary>
    /// Email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Password
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Display name (optional)
    /// </summary>
    public string? DisplayName { get; set; }
}

/// <summary>
/// Result of a registration attempt
/// </summary>
public class LocalRegistrationResult
{
    /// <summary>
    /// Whether registration succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The created user (if successful)
    /// </summary>
    public LocalUser? User { get; set; }

    /// <summary>
    /// The subject ID for the new user (if successful)
    /// </summary>
    public Guid? SubjectId { get; set; }

    /// <summary>
    /// Error code if registration failed
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Error message if registration failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Whether the user requires email verification
    /// </summary>
    public bool RequiresEmailVerification { get; set; }

    /// <summary>
    /// Whether the user requires admin approval
    /// </summary>
    public bool RequiresAdminApproval { get; set; }

    public static LocalRegistrationResult Succeeded(
        LocalUser user,
        Guid subjectId,
        bool requiresVerification,
        bool requiresApproval
    ) =>
        new()
        {
            Success = true,
            User = user,
            SubjectId = subjectId,
            RequiresEmailVerification = requiresVerification,
            RequiresAdminApproval = requiresApproval,
        };

    public static LocalRegistrationResult Failed(string errorCode, string message) =>
        new()
        {
            Success = false,
            Error = errorCode,
            ErrorMessage = message,
        };
}

/// <summary>
/// Result of an authentication attempt
/// </summary>
public class LocalAuthResult
{
    /// <summary>
    /// Whether authentication succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// The authenticated user (if successful)
    /// </summary>
    public LocalUser? User { get; set; }

    /// <summary>
    /// The associated subject (for permissions)
    /// </summary>
    public Guid? SubjectId { get; set; }

    /// <summary>
    /// Error code if authentication failed
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Error message if authentication failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of remaining login attempts before lockout
    /// </summary>
    public int? RemainingAttempts { get; set; }

    /// <summary>
    /// When the lockout ends (if locked out)
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>When <c>true</c>, the user must change their password before continuing.</summary>
    public bool RequirePasswordChange { get; set; }

    /// <summary>Returns a successful authentication result.</summary>
    /// <param name="user">The authenticated <see cref="LocalUser"/>.</param>
    /// <param name="subjectId">The subject identifier for the user.</param>
    /// <param name="requirePasswordChange">Whether the user must change their password immediately.</param>
    public static LocalAuthResult Succeeded(
        LocalUser user,
        Guid subjectId,
        bool requirePasswordChange = false
    ) =>
        new()
        {
            Success = true,
            User = user,
            SubjectId = subjectId,
            RequirePasswordChange = requirePasswordChange,
        };

    /// <summary>Returns a failed authentication result.</summary>
    /// <param name="errorCode">Machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    /// <param name="remainingAttempts">Remaining login attempts before lockout, if applicable.</param>
    /// <param name="lockedUntil">UTC time when the lockout ends, if the account is locked.</param>
    public static LocalAuthResult Failed(
        string errorCode,
        string message,
        int? remainingAttempts = null,
        DateTime? lockedUntil = null
    ) =>
        new()
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = message,
            RemainingAttempts = remainingAttempts,
            LockedUntil = lockedUntil,
        };
}

/// <summary>
/// Result of a password reset request
/// </summary>
public class PasswordResetRequestResult
{
    /// <summary>
    /// Whether the request was processed
    /// (Always true to prevent email enumeration, even if user doesn't exist)
    /// </summary>
    public bool Processed { get; set; } = true;

    /// <summary>
    /// Whether an email was sent (only for internal use, don't expose)
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// Whether admin notification is required (SMTP not configured)
    /// </summary>
    public bool AdminNotificationRequired { get; set; }

    /// <summary>
    /// Message to show user
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// A local user registered through the built-in identity provider.
/// </summary>
public class LocalUser
{
    /// <summary>Unique identifier for the local user record.</summary>
    public Guid Id { get; set; }

    /// <summary>Email address used to log in.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Optional display name shown in the UI.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Whether the user's email address has been verified.</summary>
    public bool EmailVerified { get; set; }

    /// <summary>Whether the account is currently active and permitted to log in.</summary>
    public bool IsActive { get; set; }

    /// <summary>Whether the account is awaiting administrator approval before it can be used.</summary>
    public bool PendingApproval { get; set; }

    /// <summary>When <c>true</c>, the user must change their password on next login.</summary>
    public bool RequirePasswordChange { get; set; }

    /// <summary>The subject identifier linking this user to the authorization system, or <c>null</c> if not yet linked.</summary>
    public Guid? SubjectId { get; set; }

    /// <summary>UTC timestamp of the user's last successful login, or <c>null</c> if they have never logged in.</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>UTC timestamp when the user account was created.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Filter criteria for listing <see cref="LocalUser"/> records.
/// Null properties are ignored (not applied as filters).
/// </summary>
public class LocalUserFilter
{
    /// <summary>Filter to only users pending administrator approval, or only those not pending.</summary>
    public bool? PendingApproval { get; set; }

    /// <summary>Filter to only active users, or only inactive users.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Filter to only email-verified users, or only unverified users.</summary>
    public bool? EmailVerified { get; set; }

    /// <summary>Optional free-text search term matched against email and display name.</summary>
    public string? SearchTerm { get; set; }
}

/// <summary>
/// A pending password reset request submitted by a user who cannot log in.
/// </summary>
public class PasswordResetRequest
{
    /// <summary>Unique identifier for this password reset request.</summary>
    public Guid Id { get; set; }

    /// <summary>Email address of the user requesting the reset.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name of the user, if available.</summary>
    public string? DisplayName { get; set; }

    /// <summary>IP address from which the reset was requested, if available.</summary>
    public string? RequestedFromIp { get; set; }

    /// <summary>User agent string from the requesting browser, if available.</summary>
    public string? UserAgent { get; set; }

    /// <summary>UTC timestamp when the reset request was submitted.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Result of checking email against allowlist
/// </summary>
public class AllowlistCheckResult
{
    /// <summary>
    /// Whether the email is allowed
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Whether admin approval is required
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Reason if not allowed
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Result of password validation against the configured complexity requirements.
/// </summary>
public class PasswordValidationResult
{
    /// <summary>Whether the password satisfies all requirements.</summary>
    public bool IsValid { get; set; }

    /// <summary>Human-readable error messages describing each unmet requirement.</summary>
    public List<string> Errors { get; set; } = new();

    /// <summary>Returns a successful validation result with no errors.</summary>
    public static PasswordValidationResult Valid() => new() { IsValid = true };

    /// <summary>Returns a failed validation result with the specified error messages.</summary>
    /// <param name="errors">One or more error messages describing the requirement violations.</param>
    public static PasswordValidationResult Invalid(params string[] errors) =>
        new() { IsValid = false, Errors = errors.ToList() };
}

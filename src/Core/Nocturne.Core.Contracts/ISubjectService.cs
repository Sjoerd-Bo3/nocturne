using Nocturne.Core.Models.Authorization;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for managing authentication subjects (users, devices, API keys)
/// </summary>
public interface ISubjectService
{
    /// <summary>
    /// Get a subject by ID
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>Subject if found, null otherwise</returns>
    Task<Subject?> GetSubjectByIdAsync(Guid subjectId);

    /// <summary>
    /// Get a subject by its access token hash (for API key auth)
    /// </summary>
    /// <param name="accessTokenHash">SHA-256 hash of the access token</param>
    /// <returns>Subject if found and active, null otherwise</returns>
    Task<Subject?> GetSubjectByAccessTokenHashAsync(string accessTokenHash);

    /// <summary>
    /// Find or create a subject from OIDC claims
    /// </summary>
    /// <param name="oidcSubjectId">OIDC subject identifier (sub claim)</param>
    /// <param name="issuer">OIDC issuer URL</param>
    /// <param name="email">Email from OIDC claims</param>
    /// <param name="name">Name from OIDC claims</param>
    /// <param name="defaultRoles">Default roles to assign if creating</param>
    /// <returns>Found or created subject</returns>
    Task<Subject> FindOrCreateFromOidcAsync(
        string oidcSubjectId,
        string issuer,
        string? email = null,
        string? name = null,
        IEnumerable<string>? defaultRoles = null);

    /// <summary>
    /// Create a new subject (device/API key)
    /// </summary>
    /// <param name="subject">Subject to create</param>
    /// <returns>Created subject with generated token if applicable</returns>
    Task<SubjectCreationResult> CreateSubjectAsync(Subject subject);

    /// <summary>
    /// Update a subject
    /// </summary>
    /// <param name="subject">Subject to update</param>
    /// <returns>Updated subject or null if not found</returns>
    Task<Subject?> UpdateSubjectAsync(Subject subject);

    /// <summary>
    /// Delete a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteSubjectAsync(Guid subjectId);

    /// <summary>
    /// Regenerate the access token for a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>New access token (only returned once)</returns>
    Task<string?> RegenerateAccessTokenAsync(Guid subjectId);

    /// <summary>
    /// Activate a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>True if activated, false if not found</returns>
    Task<bool> ActivateSubjectAsync(Guid subjectId);

    /// <summary>
    /// Deactivate a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>True if deactivated, false if not found</returns>
    Task<bool> DeactivateSubjectAsync(Guid subjectId);

    /// <summary>
    /// Get all subjects with optional filtering
    /// </summary>
    /// <param name="filter">Optional filter criteria</param>
    /// <returns>List of subjects</returns>
    Task<List<Subject>> GetSubjectsAsync(SubjectFilter? filter = null);

    /// <summary>
    /// Get roles assigned to a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>List of role names</returns>
    Task<List<string>> GetSubjectRolesAsync(Guid subjectId);

    /// <summary>
    /// Get all permissions for a subject (aggregated from all roles)
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <returns>List of permissions</returns>
    Task<List<string>> GetSubjectPermissionsAsync(Guid subjectId);

    /// <summary>
    /// Assign a role to a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="roleName">Role name</param>
    /// <param name="assignedBy">Who assigned the role (subject ID)</param>
    /// <returns>True if assigned, false if already assigned or not found</returns>
    Task<bool> AssignRoleAsync(Guid subjectId, string roleName, Guid? assignedBy = null);

    /// <summary>
    /// Remove a role from a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="roleName">Role name</param>
    /// <returns>True if removed, false if not assigned or not found</returns>
    Task<bool> RemoveRoleAsync(Guid subjectId, string roleName);

    /// <summary>
    /// Check if a subject has a specific permission
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="permission">Permission to check (Shiro-style)</param>
    /// <returns>True if subject has the permission</returns>
    Task<bool> HasPermissionAsync(Guid subjectId, string permission);

    /// <summary>
    /// Update last login timestamp for a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    Task UpdateLastLoginAsync(Guid subjectId);

    /// <summary>
    /// Initialize the Public system subject for unauthenticated access
    /// </summary>
    /// <returns>The Public subject</returns>
    Task<Subject?> InitializePublicSubjectAsync();

    /// <summary>
    /// Check whether a subject has at least one alternative authentication method
    /// beyond the specified type. Used to prevent removal of the last sign-in method.
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="excluding">The auth method type being removed</param>
    /// <returns>Guard result indicating whether alternatives exist</returns>
    Task<AuthMethodGuardResult> HasAlternativeAuthMethodAsync(Guid subjectId, AuthMethodType excluding);
}

/// <summary>
/// Result of creating a subject
/// </summary>
public class SubjectCreationResult
{
    /// <summary>
    /// Created subject
    /// </summary>
    public required Subject Subject { get; set; }

    /// <summary>
    /// Plain-text access token (only returned once, for API key subjects)
    /// </summary>
    public string? AccessToken { get; set; }
}

/// <summary>
/// Types of authentication methods a subject can have
/// </summary>
public enum AuthMethodType
{
    Passkey,
    Totp,
    Oidc,
}

/// <summary>
/// Result of checking whether a subject has alternative authentication methods
/// </summary>
/// <param name="HasAlternative">True if the subject has at least one auth method beyond the excluded type</param>
/// <param name="LastRemainingMethodName">If no alternatives exist, the name of the last remaining method of the excluded type</param>
/// <param name="LastRemainingMethodType">If no alternatives exist, the type of the last remaining method</param>
public record AuthMethodGuardResult(
    bool HasAlternative,
    string? LastRemainingMethodName,
    AuthMethodType? LastRemainingMethodType);

/// <summary>
/// Filter criteria for querying subjects
/// </summary>
public class SubjectFilter
{
    /// <summary>
    /// Filter by subject type
    /// </summary>
    public SubjectType? Type { get; set; }

    /// <summary>
    /// Filter by active status
    /// </summary>
    public bool? IsActive { get; set; }

    /// <summary>
    /// Filter by OIDC issuer
    /// </summary>
    public string? OidcIssuer { get; set; }

    /// <summary>
    /// Search by name (contains)
    /// </summary>
    public string? NameContains { get; set; }

    /// <summary>
    /// Search by email (contains)
    /// </summary>
    public string? EmailContains { get; set; }

    /// <summary>
    /// Filter by role
    /// </summary>
    public string? HasRole { get; set; }

    /// <summary>
    /// Maximum number of results
    /// </summary>
    public int Limit { get; set; } = 100;

    /// <summary>
    /// Number of results to skip
    /// </summary>
    public int Offset { get; set; } = 0;
}

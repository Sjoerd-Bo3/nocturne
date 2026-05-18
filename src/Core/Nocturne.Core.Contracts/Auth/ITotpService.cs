namespace Nocturne.Core.Contracts.Auth;

/// <summary>
/// Service for managing TOTP (Time-based One-Time Password) two-factor authentication.
/// </summary>
/// <seealso cref="IPasskeyService"/>
/// <seealso cref="IRecoveryCodeService"/>
/// <seealso cref="ISubjectService"/>
public interface ITotpService
{
    /// <summary>Generates a TOTP secret and provisioning URI for scanning by an authenticator app.</summary>
    Task<TotpSetupResult> GenerateSetupAsync(Guid subjectId, string username);

    /// <summary>Verifies a TOTP code against the pending setup challenge and registers the credential.</summary>
    Task<TotpCredentialResult> CompleteSetupAsync(string code, string label, string challengeToken);

    /// <summary>Verifies a TOTP code for login and returns the authenticated subject, or null if invalid.</summary>
    Task<TotpLoginResult?> VerifyLoginAsync(string username, string code);

    /// <summary>Returns all registered TOTP credentials for the specified subject.</summary>
    Task<List<TotpCredentialInfo>> GetCredentialsAsync(Guid subjectId);

    /// <summary>Removes a TOTP credential from the specified subject.</summary>
    Task RemoveCredentialAsync(Guid credentialId, Guid subjectId);

    /// <summary>Returns the number of TOTP credentials registered to the specified subject.</summary>
    Task<int> GetCredentialCountAsync(Guid subjectId);
}

/// <summary>Result of generating a TOTP setup challenge.</summary>
/// <param name="ProvisioningUri">otpauth:// URI for scanning by an authenticator app.</param>
/// <param name="Base32Secret">Base32-encoded TOTP secret for manual entry.</param>
/// <param name="ChallengeToken">Opaque token used to correlate the challenge on completion.</param>
public record TotpSetupResult(string ProvisioningUri, string Base32Secret, string ChallengeToken);

/// <summary>Result of successfully registering a new TOTP credential.</summary>
/// <param name="CredentialId">The newly registered credential's ID.</param>
/// <param name="SubjectId">The subject the credential was registered to.</param>
public record TotpCredentialResult(Guid CredentialId, Guid SubjectId);

/// <summary>Result of a successful TOTP login verification.</summary>
/// <param name="SubjectId">The authenticated subject's ID.</param>
/// <param name="Username">The authenticated subject's username.</param>
/// <param name="DisplayName">The authenticated subject's display name.</param>
public record TotpLoginResult(Guid SubjectId, string Username, string DisplayName);

/// <summary>Summary information about a registered TOTP credential.</summary>
/// <param name="Id">The credential's ID.</param>
/// <param name="Label">User-assigned label for the authenticator, if any.</param>
/// <param name="CreatedAt">When the credential was registered.</param>
/// <param name="LastUsedAt">When the credential was last used for authentication, if ever.</param>
public record TotpCredentialInfo(Guid Id, string? Label, DateTime CreatedAt, DateTime? LastUsedAt);

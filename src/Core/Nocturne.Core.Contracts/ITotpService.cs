namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for managing TOTP (Time-based One-Time Password) two-factor authentication
/// </summary>
public interface ITotpService
{
    Task<TotpSetupResult> GenerateSetupAsync(Guid subjectId, string username);
    Task<TotpCredentialResult> CompleteSetupAsync(string code, string label, string challengeToken);
    Task<TotpLoginResult?> VerifyLoginAsync(string username, string code);
    Task<List<TotpCredentialInfo>> GetCredentialsAsync(Guid subjectId);
    Task RemoveCredentialAsync(Guid credentialId, Guid subjectId);
    Task<int> GetCredentialCountAsync(Guid subjectId);
}

public record TotpSetupResult(string ProvisioningUri, string Base32Secret, string ChallengeToken);
public record TotpCredentialResult(Guid CredentialId, Guid SubjectId);
public record TotpLoginResult(Guid SubjectId, string Username, string DisplayName);
public record TotpCredentialInfo(Guid Id, string? Label, DateTime CreatedAt, DateTime? LastUsedAt);

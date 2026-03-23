namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for managing single-use recovery codes for break-glass account access
/// </summary>
public interface IRecoveryCodeService
{
    Task<List<string>> GenerateCodesAsync(Guid subjectId);
    Task<bool> VerifyAndConsumeAsync(Guid subjectId, string code);
    Task<int> GetRemainingCountAsync(Guid subjectId);
    Task<bool> HasCodesAsync(Guid subjectId);
}

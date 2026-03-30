namespace Nocturne.Core.Contracts.Multitenancy;

public interface IMemberInviteService
{
    Task<MemberInviteResult> CreateInviteAsync(
        Guid tenantId,
        Guid createdBySubjectId,
        List<Guid> roleIds,
        List<string>? directPermissions = null,
        string? label = null,
        int expiresInDays = 7,
        int? maxUses = null,
        bool limitTo24Hours = false);

    Task<MemberInviteInfo?> GetInviteByTokenAsync(string token);
    Task<AcceptMemberInviteResult> AcceptInviteAsync(string token, Guid acceptingSubjectId);
    Task<List<MemberInviteInfo>> GetInvitesForTenantAsync(Guid tenantId);
    Task<bool> RevokeInviteAsync(Guid inviteId, Guid tenantId);
}

public record MemberInviteResult(
    Guid Id,
    string Token,
    string InviteUrl,
    DateTime ExpiresAt);

public record MemberInviteInfo(
    Guid Id,
    Guid TenantId,
    string TenantName,
    string CreatedByName,
    List<Guid> RoleIds,
    List<string>? DirectPermissions,
    string? Label,
    bool LimitTo24Hours,
    DateTime ExpiresAt,
    int? MaxUses,
    int UseCount,
    bool IsValid,
    bool IsExpired,
    bool IsRevoked,
    DateTime CreatedAt,
    List<InviteUsageInfo> UsedBy);

public record InviteUsageInfo(
    Guid SubjectId,
    string? Name,
    DateTime JoinedAt);

public record AcceptMemberInviteResult(
    bool Success,
    string? ErrorCode = null,
    string? ErrorDescription = null,
    Guid? MembershipId = null);

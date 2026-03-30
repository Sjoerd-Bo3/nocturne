namespace Nocturne.Core.Contracts.Multitenancy;

/// <summary>
/// Service for checking tenant membership.
/// Used by auth handlers to verify a subject belongs to the resolved tenant.
/// </summary>
public interface ITenantMemberService
{
    Task<bool> IsMemberAsync(Guid subjectId, Guid tenantId, CancellationToken ct = default);
    Task<List<Guid>> GetTenantIdsForSubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<int> GetMemberCountAsync(Guid tenantId, CancellationToken ct = default);
}

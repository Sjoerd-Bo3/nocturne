namespace Nocturne.Core.Contracts.Multitenancy;

public interface ITenantRoleService
{
    Task<List<TenantRoleDto>> GetRolesAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantRoleDto?> GetRoleByIdAsync(Guid roleId, CancellationToken ct = default);
    Task<TenantRoleDto> CreateRoleAsync(Guid tenantId, string name, string? description, List<string> permissions, CancellationToken ct = default);
    Task<TenantRoleDto> UpdateRoleAsync(Guid roleId, string name, string? description, List<string> permissions, CancellationToken ct = default);
    Task<DeleteRoleResult> DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
    Task SeedRolesForTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<List<string>> GetEffectivePermissionsAsync(Guid memberId, CancellationToken ct = default);
}

public record TenantRoleDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    List<string> Permissions,
    bool IsSystem,
    int MemberCount,
    DateTime SysCreatedAt
);

public record DeleteRoleResult(bool Success, string? ErrorCode, string? ErrorDescription);

namespace Nocturne.Core.Contracts.Multitenancy;

public interface ITenantService
{
    Task<TenantDto> CreateAsync(string slug, string displayName, string? apiSecret = null, CancellationToken ct = default);
    Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default);
    Task<TenantDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantDto> UpdateAsync(Guid id, string displayName, bool isActive, CancellationToken ct = default);
    Task AddMemberAsync(Guid tenantId, Guid subjectId, string role, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid tenantId, Guid subjectId, CancellationToken ct = default);
    Task<List<TenantDto>> GetTenantsForSubjectAsync(Guid subjectId, CancellationToken ct = default);
    Task<SlugValidationResult> ValidateSlugAsync(string slug, CancellationToken ct = default);
}

public record TenantDto(Guid Id, string Slug, string DisplayName, bool IsActive, bool IsDefault, DateTime SysCreatedAt);

public record TenantDetailDto(Guid Id, string Slug, string DisplayName, bool IsActive, bool IsDefault, DateTime SysCreatedAt, List<TenantMemberDto> Members);

public record TenantMemberDto(Guid SubjectId, string Role, DateTime SysCreatedAt);

public record SlugValidationResult(bool IsValid, string? Message = null);

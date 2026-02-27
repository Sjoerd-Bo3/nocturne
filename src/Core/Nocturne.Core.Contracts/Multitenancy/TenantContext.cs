namespace Nocturne.Core.Contracts.Multitenancy;

/// <summary>
/// Resolved tenant information for the current request
/// </summary>
public record TenantContext(Guid TenantId, string Slug, string DisplayName, bool IsActive);

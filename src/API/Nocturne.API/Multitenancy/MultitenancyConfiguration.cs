namespace Nocturne.API.Multitenancy;

/// <summary>
/// Configuration for the multitenancy system.
/// Bound from appsettings.json section "Multitenancy".
/// </summary>
public class MultitenancyConfiguration
{
    public const string SectionName = "Multitenancy";

    /// <summary>
    /// Base domain for subdomain tenant resolution.
    /// e.g. "nocturnecgm.com" - requests to rhys.nocturnecgm.com resolve tenant "rhys".
    /// When null or empty, all requests resolve to the default tenant (self-hosted mode).
    /// </summary>
    public string? BaseDomain { get; set; }

    /// <summary>
    /// Slug used for the auto-created default tenant.
    /// </summary>
    public string DefaultTenantSlug { get; set; } = "default";
}

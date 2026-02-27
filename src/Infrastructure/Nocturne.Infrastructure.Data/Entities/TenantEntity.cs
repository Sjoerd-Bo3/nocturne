using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Represents an isolated tenant (patient data silo) in the multitenant system.
/// Each tenant has its own subdomain and isolated clinical data.
/// </summary>
[Table("tenants")]
public class TenantEntity
{
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Subdomain identifier, e.g. "rhys" for rhys.nocturnecgm.com
    /// </summary>
    [Column("slug")]
    [MaxLength(64)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable display name
    /// </summary>
    [Column("display_name")]
    [MaxLength(256)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Per-tenant API secret hash (SHA1, Nightscout-compatible)
    /// </summary>
    [Column("api_secret_hash")]
    [MaxLength(128)]
    public string? ApiSecretHash { get; set; }

    /// <summary>
    /// Whether this tenant is active. Inactive tenants return 403.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this is the auto-created default tenant for self-hosted deployments
    /// </summary>
    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; } = DateTime.UtcNow;

    [Column("sys_updated_at")]
    public DateTime SysUpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<TenantMemberEntity> Members { get; set; } = [];
}

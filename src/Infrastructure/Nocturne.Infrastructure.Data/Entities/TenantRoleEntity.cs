using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

[Table("tenant_roles")]
public class TenantRoleEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public TenantEntity Tenant { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [MaxLength(500)]
    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("permissions", TypeName = "jsonb")]
    public List<string> Permissions { get; set; } = [];

    [Required]
    [Column("is_system")]
    public bool IsSystem { get; set; }

    [Required]
    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; }

    [Required]
    [Column("sys_updated_at")]
    public DateTime SysUpdatedAt { get; set; }

    public List<TenantMemberRoleEntity> MemberRoles { get; set; } = [];
}

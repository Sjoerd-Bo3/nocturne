using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

[Table("tenant_member_roles")]
public class TenantMemberRoleEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Required]
    [Column("tenant_member_id")]
    public Guid TenantMemberId { get; set; }

    [ForeignKey(nameof(TenantMemberId))]
    public TenantMemberEntity TenantMember { get; set; } = null!;

    [Required]
    [Column("tenant_role_id")]
    public Guid TenantRoleId { get; set; }

    [ForeignKey(nameof(TenantRoleId))]
    public TenantRoleEntity TenantRole { get; set; } = null!;

    [Required]
    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Join entity linking subjects to tenants (many-to-many).
/// </summary>
[Table("tenant_members")]
public class TenantMemberEntity
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    [Column("role")]
    [MaxLength(32)]
    public string Role { get; set; } = TenantRole.Follower;

    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; } = DateTime.UtcNow;

    [Column("sys_updated_at")]
    public DateTime SysUpdatedAt { get; set; } = DateTime.UtcNow;

    [Column("scopes", TypeName = "jsonb")]
    public List<string>? Scopes { get; set; }

    [Column("label")]
    [MaxLength(255)]
    public string? Label { get; set; }

    [Column("limit_to_24_hours")]
    public bool LimitTo24Hours { get; set; } = false;

    [Column("created_from_invite_id")]
    public Guid? CreatedFromInviteId { get; set; }

    [Column("last_used_at")]
    public DateTime? LastUsedAt { get; set; }

    [Column("last_used_ip")]
    [MaxLength(45)]
    public string? LastUsedIp { get; set; }

    [Column("last_used_user_agent")]
    public string? LastUsedUserAgent { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public SubjectEntity? Subject { get; set; }
    public MemberInviteEntity? CreatedFromInvite { get; set; }
    public List<TenantMemberRoleEntity> MemberRoles { get; set; } = [];
}

/// <summary>
/// Roles a subject can have within a tenant
/// </summary>
public static class TenantRole
{
    public const string Owner = "owner";
    public const string Admin = "admin";
    public const string Caretaker = "caretaker";
    public const string Follower = "follower";
}

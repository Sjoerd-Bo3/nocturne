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
    public string Role { get; set; } = TenantRole.ReadOnly;

    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; } = DateTime.UtcNow;

    [Column("sys_updated_at")]
    public DateTime SysUpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public TenantEntity? Tenant { get; set; }
    public SubjectEntity? Subject { get; set; }
}

/// <summary>
/// Roles a subject can have within a tenant
/// </summary>
public static class TenantRole
{
    public const string Owner = "owner";
    public const string Member = "member";
    public const string Caretaker = "caretaker";
    public const string ReadOnly = "readonly";
}

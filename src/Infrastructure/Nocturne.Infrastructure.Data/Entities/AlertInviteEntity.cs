using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A shareable invite token that grants a follower permission to receive alerts
/// and optionally acknowledge them. Scoped to a specific escalation step.
/// </summary>
[Table("alert_invites")]
public class AlertInviteEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    /// <summary>
    /// Unique, URL-safe invite token.
    /// </summary>
    [Column("token")]
    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    /// <summary>
    /// Permission scope: "view_acknowledge" | "view_only"
    /// </summary>
    [Column("permission_scope")]
    [MaxLength(32)]
    public string PermissionScope { get; set; } = "view_acknowledge";

    [Column("is_used")]
    public bool IsUsed { get; set; }

    [Column("used_by")]
    public Guid? UsedBy { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}

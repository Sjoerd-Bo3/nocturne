using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A delivery channel attached to an escalation step (e.g. push, SMS, email, webhook).
/// </summary>
[Table("alert_step_channels")]
public class AlertStepChannelEntity : ITenantScoped
{
    /// <summary>
    /// Unique identifier for the alert step channel
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The unique identifier of the tenant this step channel belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identifier of the escalation step this channel is attached to
    /// </summary>
    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    /// <summary>
    /// Channel type identifier (e.g. "push", "sms", "email", "webhook").
    /// </summary>
    [Column("channel_type")]
    [MaxLength(32)]
    public string ChannelType { get; set; } = string.Empty;

    /// <summary>
    /// Destination address (phone number, email, webhook URL, device token, etc.)
    /// </summary>
    [Column("destination")]
    [MaxLength(512)]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable label for the destination (e.g. "Mom's phone").
    /// </summary>
    [Column("destination_label")]
    [MaxLength(128)]
    public string? DestinationLabel { get; set; }

    /// <summary>
    /// When the channel configuration was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation

    /// <summary>
    /// Navigation property to the associated escalation step
    /// </summary>
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}

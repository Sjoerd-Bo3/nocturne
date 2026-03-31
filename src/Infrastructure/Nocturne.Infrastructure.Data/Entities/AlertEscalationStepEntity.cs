using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A single step in an escalation chain. After DelaySeconds with no acknowledgement,
/// the engine advances to the next step.
/// </summary>
[Table("alert_escalation_steps")]
public class AlertEscalationStepEntity : ITenantScoped
{
    /// <summary>
    /// Unique identifier for the alert escalation step
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The unique identifier of the tenant this escalation step belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identifier of the alert schedule this step belongs to
    /// </summary>
    [Column("alert_schedule_id")]
    public Guid AlertScheduleId { get; set; }

    /// <summary>
    /// The position of this step in the escalation chain
    /// </summary>
    [Column("step_order")]
    public int StepOrder { get; set; }

    /// <summary>
    /// Seconds to wait before escalating to the next step.
    /// </summary>
    [Column("delay_seconds")]
    public int DelaySeconds { get; set; }

    /// <summary>
    /// When the escalation step was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation

    /// <summary>
    /// Navigation property to the associated alert schedule
    /// </summary>
    public AlertScheduleEntity? AlertSchedule { get; set; }

    /// <summary>
    /// Collection of delivery channels defined for this step
    /// </summary>
    public ICollection<AlertStepChannelEntity> Channels { get; set; } = [];
}

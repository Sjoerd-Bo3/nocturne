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
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_schedule_id")]
    public Guid AlertScheduleId { get; set; }

    [Column("step_order")]
    public int StepOrder { get; set; }

    /// <summary>
    /// Seconds to wait before escalating to the next step.
    /// </summary>
    [Column("delay_seconds")]
    public int DelaySeconds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertScheduleEntity? AlertSchedule { get; set; }
    public ICollection<AlertStepChannelEntity> Channels { get; set; } = [];
}

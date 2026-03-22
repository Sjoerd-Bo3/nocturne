using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A schedule-bound instance of an alert within an excursion.
/// Tracks which escalation step is active and when to escalate next.
/// </summary>
[Table("alert_instances")]
public class AlertInstanceEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_excursion_id")]
    public Guid AlertExcursionId { get; set; }

    [Column("alert_schedule_id")]
    public Guid AlertScheduleId { get; set; }

    [Column("current_step_order")]
    public int CurrentStepOrder { get; set; }

    /// <summary>
    /// Instance lifecycle status: "triggered" | "escalating" | "acknowledged" | "resolved"
    /// </summary>
    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "triggered";

    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// When the engine should next attempt escalation to the following step.
    /// </summary>
    [Column("next_escalation_at")]
    public DateTime? NextEscalationAt { get; set; }

    // Navigation
    public AlertExcursionEntity? AlertExcursion { get; set; }
    public AlertScheduleEntity? AlertSchedule { get; set; }
}

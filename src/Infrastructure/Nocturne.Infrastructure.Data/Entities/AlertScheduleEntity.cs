using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A time-of-day / day-of-week schedule window for an alert rule.
/// Each schedule owns its own escalation chain.
/// </summary>
[Table("alert_schedules")]
public class AlertScheduleEntity : ITenantScoped
{
    /// <summary>
    /// Unique identifier for the alert schedule
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the tenant this alert schedule belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identifier of the alert rule this schedule belongs to
    /// </summary>
    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    /// <summary>
    /// Display name of the schedule
    /// </summary>
    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Whether this is the default schedule for the rule
    /// </summary>
    [Column("is_default")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// JSONB int array of ISO day-of-week values (1=Mon..7=Sun). Null means all days.
    /// </summary>
    [Column("days_of_week", TypeName = "jsonb")]
    public string? DaysOfWeek { get; set; }

    /// <summary>
    /// Time of day when the schedule becomes active
    /// </summary>
    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    /// <summary>
    /// Time of day when the schedule becomes inactive
    /// </summary>
    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    /// <summary>
    /// Timezone for the start and end times
    /// </summary>
    [Column("timezone")]
    [MaxLength(64)]
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// When the alert schedule was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the alert schedule was last updated
    /// </summary>
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation

    /// <summary>
    /// Navigation property to the associated alert rule
    /// </summary>
    public AlertRuleEntity? AlertRule { get; set; }

    /// <summary>
    /// Collection of escalation steps defined for this schedule
    /// </summary>
    public ICollection<AlertEscalationStepEntity> EscalationSteps { get; set; } = [];
}

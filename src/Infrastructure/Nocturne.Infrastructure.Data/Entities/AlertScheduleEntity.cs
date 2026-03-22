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
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = "Default";

    [Column("is_default")]
    public bool IsDefault { get; set; }

    /// <summary>
    /// JSONB int array of ISO day-of-week values (1=Mon..7=Sun). Null means all days.
    /// </summary>
    [Column("days_of_week", TypeName = "jsonb")]
    public string? DaysOfWeek { get; set; }

    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    [Column("timezone")]
    [MaxLength(64)]
    public string Timezone { get; set; } = "UTC";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertRuleEntity? AlertRule { get; set; }
    public ICollection<AlertEscalationStepEntity> EscalationSteps { get; set; } = [];
}

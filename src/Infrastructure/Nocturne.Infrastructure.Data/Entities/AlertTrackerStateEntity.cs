using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Per-rule state machine tracker. 1:1 with AlertRuleEntity (PK = AlertRuleId).
/// States: idle -> confirming -> active -> hysteresis -> idle.
/// </summary>
[Table("alert_tracker_state")]
public class AlertTrackerStateEntity : ITenantScoped
{
    /// <summary>
    /// Primary key — same as the owning AlertRule's Id (1:1 relationship).
    /// </summary>
    [Key]
    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Current state: "idle" | "confirming" | "active" | "hysteresis"
    /// </summary>
    [Column("state")]
    [MaxLength(16)]
    public string State { get; set; } = "idle";

    /// <summary>
    /// Number of consecutive confirming readings observed.
    /// </summary>
    [Column("confirmation_count")]
    public int ConfirmationCount { get; set; }

    /// <summary>
    /// FK to the currently-open excursion, if any.
    /// </summary>
    [Column("active_excursion_id")]
    public Guid? ActiveExcursionId { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertRuleEntity? AlertRule { get; set; }
    public AlertExcursionEntity? ActiveExcursion { get; set; }
}

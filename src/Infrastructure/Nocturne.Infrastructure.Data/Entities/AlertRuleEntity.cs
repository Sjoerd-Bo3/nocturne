using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A composable alert rule with condition tree, hysteresis, and confirmation settings.
/// Each rule owns schedules, which own escalation chains.
/// </summary>
[Table("alert_rules")]
public class AlertRuleEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(512)]
    public string? Description { get; set; }

    /// <summary>
    /// Condition type: "threshold" | "rate_of_change" | "signal_loss" | "composite"
    /// </summary>
    [Column("condition_type")]
    [MaxLength(32)]
    public string ConditionType { get; set; } = string.Empty;

    /// <summary>
    /// JSONB condition parameters (thresholds, rates, durations, composite children, etc.)
    /// </summary>
    [Column("condition_params", TypeName = "jsonb")]
    public string ConditionParams { get; set; } = "{}";

    /// <summary>
    /// Minutes the condition must remain cleared before transitioning back to idle.
    /// </summary>
    [Column("hysteresis_minutes")]
    public int HysteresisMinutes { get; set; }

    /// <summary>
    /// Number of consecutive readings that must satisfy the condition before firing.
    /// </summary>
    [Column("confirmation_readings")]
    public int ConfirmationReadings { get; set; } = 1;

    /// <summary>
    /// Alert severity. "normal" or "critical". Critical alerts bypass quiet hours.
    /// </summary>
    [Column("severity")]
    [MaxLength(16)]
    public string Severity { get; set; } = "normal";

    /// <summary>
    /// Client-side presentation config (audio, visual, snooze). Stored as JSONB.
    /// The server stores this but does not make decisions based on it.
    /// </summary>
    [Column("client_configuration", TypeName = "jsonb")]
    public string ClientConfiguration { get; set; } = "{}";

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<AlertScheduleEntity> Schedules { get; set; } = [];
    public AlertTrackerStateEntity? TrackerState { get; set; }
}

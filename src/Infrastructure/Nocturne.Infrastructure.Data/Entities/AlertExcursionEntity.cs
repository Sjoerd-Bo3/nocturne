using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A single continuous excursion (out-of-range episode) for a rule.
/// Spans from first trigger to resolution + hysteresis clear.
/// </summary>
[Table("alert_excursions")]
public class AlertExcursionEntity : ITenantScoped, IAuditable
{
    /// <summary>
    /// Unique identifier for the alert excursion
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The unique identifier of the tenant this excursion belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identifier of the alert rule that triggered this excursion
    /// </summary>
    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    /// <summary>
    /// When the excursion condition was first met
    /// </summary>
    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the excursion condition cleared
    /// </summary>
    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    /// <summary>
    /// When the excursion was acknowledged by a user
    /// </summary>
    [Column("acknowledged_at")]
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Who acknowledged the excursion (subject display name or external identifier).
    /// </summary>
    [Column("acknowledged_by")]
    [MaxLength(256)]
    public string? AcknowledgedBy { get; set; }

    /// <summary>
    /// When hysteresis countdown began (condition cleared but waiting for hysteresis window).
    /// </summary>
    [Column("hysteresis_started_at")]
    public DateTime? HysteresisStartedAt { get; set; }

    // Navigation

    /// <summary>
    /// Navigation property to the associated alert rule
    /// </summary>
    public AlertRuleEntity? AlertRule { get; set; }

    /// <summary>
    /// Collection of alert instances triggered during this excursion
    /// </summary>
    public ICollection<AlertInstanceEntity> Instances { get; set; } = [];
}

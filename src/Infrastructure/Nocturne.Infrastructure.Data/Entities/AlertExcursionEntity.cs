using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A single continuous excursion (out-of-range episode) for a rule.
/// Spans from first trigger to resolution + hysteresis clear.
/// </summary>
[Table("alert_excursions")]
public class AlertExcursionEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

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
    public AlertRuleEntity? AlertRule { get; set; }
    public ICollection<AlertInstanceEntity> Instances { get; set; } = [];
}

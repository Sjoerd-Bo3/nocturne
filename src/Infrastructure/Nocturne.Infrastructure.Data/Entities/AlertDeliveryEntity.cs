using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// A single delivery attempt for an alert instance through a specific channel.
/// Tracks payload, delivery status, platform IDs for threading, and retries.
/// </summary>
[Table("alert_deliveries")]
public class AlertDeliveryEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_instance_id")]
    public Guid AlertInstanceId { get; set; }

    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    [Column("channel_type")]
    [MaxLength(32)]
    public string ChannelType { get; set; } = string.Empty;

    [Column("destination")]
    [MaxLength(512)]
    public string Destination { get; set; } = string.Empty;

    /// <summary>
    /// JSONB rendered payload sent to the channel adapter.
    /// </summary>
    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    /// <summary>
    /// Delivery lifecycle status: "pending" | "delivered" | "failed" | "expired"
    /// </summary>
    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "pending";

    /// <summary>
    /// Platform-specific message ID for update/thread operations.
    /// </summary>
    [Column("platform_message_id")]
    [MaxLength(256)]
    public string? PlatformMessageId { get; set; }

    /// <summary>
    /// Platform-specific thread ID for grouped messages.
    /// </summary>
    [Column("platform_thread_id")]
    [MaxLength(256)]
    public string? PlatformThreadId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }

    // Navigation
    public AlertInstanceEntity? AlertInstance { get; set; }
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}

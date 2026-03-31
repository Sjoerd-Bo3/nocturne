using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Links a Nocturne user to a chat platform identity (Discord, Telegram, etc.)
/// for bot-mediated alert delivery and glucose queries.
/// </summary>
[Table("chat_identity_links")]
public class ChatIdentityLinkEntity : ITenantScoped
{
    /// <summary>
    /// Unique identifier for the chat identity link
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Identifier of the tenant this link belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Identifier of the Nocturne user associated with the chat identity
    /// </summary>
    [Column("nocturne_user_id")]
    public Guid NocturneUserId { get; set; }

    /// <summary>
    /// The chat platform (e.g., "discord", "telegram")
    /// </summary>
    [Column("platform")]
    [MaxLength(16)]
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// The user's unique identifier on the chat platform
    /// </summary>
    [Column("platform_user_id")]
    [MaxLength(256)]
    public string PlatformUserId { get; set; } = string.Empty;

    /// <summary>
    /// The specific channel identifier on the platform, if applicable
    /// </summary>
    [Column("platform_channel_id")]
    [MaxLength(256)]
    public string? PlatformChannelId { get; set; }

    /// <summary>
    /// The preferred glucose unit for display on this platform
    /// </summary>
    [Column("display_unit")]
    [MaxLength(8)]
    public string DisplayUnit { get; set; } = "mg/dL";

    /// <summary>
    /// Whether the chat identity link is currently active
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// When the link was created
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the link was revoked
    /// </summary>
    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }
}

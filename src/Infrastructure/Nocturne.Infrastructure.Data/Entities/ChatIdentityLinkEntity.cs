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
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("nocturne_user_id")]
    public Guid NocturneUserId { get; set; }

    [Column("platform")]
    [MaxLength(16)]
    public string Platform { get; set; } = string.Empty;

    [Column("platform_user_id")]
    [MaxLength(256)]
    public string PlatformUserId { get; set; } = string.Empty;

    [Column("platform_channel_id")]
    [MaxLength(256)]
    public string? PlatformChannelId { get; set; }

    [Column("display_unit")]
    [MaxLength(8)]
    public string DisplayUnit { get; set; } = "mg/dL";

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }
}

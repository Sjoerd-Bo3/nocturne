using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// PostgreSQL entity for storing custom alert sound files (e.g., MP3/WAV)
/// </summary>
[Table("alert_custom_sounds")]
public class AlertCustomSoundEntity : ITenantScoped
{
    /// <summary>
    /// Unique identifier for the custom sound record
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// The unique identifier of the tenant this custom sound belongs to
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Display name of the sound file
    /// </summary>
    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the sound file (e.g., "audio/mpeg")
    /// </summary>
    [Column("mime_type")]
    [MaxLength(64)]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Raw binary data of the sound file
    /// </summary>
    [Column("data")]
    public byte[] Data { get; set; } = [];

    /// <summary>
    /// Size of the sound file in bytes
    /// </summary>
    [Column("file_size")]
    public int FileSize { get; set; }

    /// <summary>
    /// When the custom sound was uploaded
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

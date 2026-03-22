using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

[Table("alert_custom_sounds")]
public class AlertCustomSoundEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Column("mime_type")]
    [MaxLength(64)]
    public string MimeType { get; set; } = string.Empty;

    [Column("data")]
    public byte[] Data { get; set; } = [];

    [Column("file_size")]
    public int FileSize { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

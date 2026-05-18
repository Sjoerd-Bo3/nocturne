using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Stores avatar image data for a subject. Not tenant-scoped — subjects are platform-level.
/// </summary>
[Table("subject_avatars")]
public class SubjectAvatarEntity
{
    /// <summary>Primary key (UUID v7).</summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>FK to the subject who owns this avatar.</summary>
    [Column("subject_id")]
    public Guid SubjectId { get; set; }

    /// <summary>Raw image bytes (WebP by default after server-side conversion).</summary>
    [Column("data")]
    public byte[] Data { get; set; } = [];

    /// <summary>MIME type of the stored image (e.g. "image/webp").</summary>
    [Column("content_type")]
    [MaxLength(64)]
    public string ContentType { get; set; } = "image/webp";

    /// <summary>Size of the image in bytes.</summary>
    [Column("file_size")]
    public int FileSize { get; set; }

    /// <summary>When this avatar was uploaded or last replaced.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Navigation to the owning subject.</summary>
    // Navigation
    public SubjectEntity Subject { get; set; } = null!;
}

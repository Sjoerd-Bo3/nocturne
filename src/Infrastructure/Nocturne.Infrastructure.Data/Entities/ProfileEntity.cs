using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// PostgreSQL entity for Profile
/// Maps to Nocturne.Core.Models.Profile
/// </summary>
[Table("profiles")]
public class ProfileEntity : ITenantScoped
{
    /// <summary>
    /// Identifier of the tenant this profile record belongs to
    /// </summary>
    /// <summary>
    /// The unique identifier of the tenant this record belongs to.
    /// </summary>
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Primary key - UUID Version 7 for time-ordered, globally unique identification
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Original MongoDB ObjectId as string for reference/migration tracking
    /// </summary>
    [Column("original_id")]
    [MaxLength(24)]
    public string? OriginalId { get; set; }

    /// <summary>
    /// Name of the default profile in the store
    /// </summary>
    [Column("default_profile")]
    [MaxLength(100)]
    public string DefaultProfile { get; set; } = "Default";

    /// <summary>
    /// Start date for this profile record (ISO string)
    /// </summary>
    [Column("start_date")]
    [MaxLength(50)]
    public string StartDate { get; set; } = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    /// <summary>
    /// Time in milliseconds since Unix epoch
    /// </summary>
    [Column("mills")]
    public long Mills { get; set; }

    /// <summary>
    /// When this profile was created (ISO string)
    /// </summary>
    [Column("created_at")]
    [MaxLength(50)]
    public string? CreatedAt { get; set; }

    /// <summary>
    /// Units used for blood glucose values (mg/dL or mmol/L)
    /// </summary>
    [Column("units")]
    [MaxLength(10)]
    public string Units { get; set; } = "mg/dl";

    /// <summary>
    /// Complete profile store as JSON (contains all profiles data)
    /// </summary>
    [Column("store_json")]
    public string StoreJson { get; set; } = "{}";

    /// <summary>
    /// Timestamp when this entity was created in PostgreSQL
    /// </summary>
    [Column("created_at_pg")]
    public DateTime CreatedAtPg { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Timestamp when this entity was last updated in PostgreSQL
    /// </summary>
    [Column("updated_at_pg")]
    public DateTime UpdatedAtPg { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who entered this profile (e.g., "Loop")
    /// </summary>
    [Column("entered_by")]
    [MaxLength(100)]
    public string? EnteredBy { get; set; }

    /// <summary>
    /// Loop-specific settings stored as JSON
    /// Contains device tokens, dosing settings, override presets, etc.
    /// </summary>
    [Column("loop_settings_json", TypeName = "jsonb")]
    public string? LoopSettingsJson { get; set; }

    /// <summary>
    /// Additional properties from import (stored as JSON)
    /// </summary>
    [Column("additional_properties", TypeName = "jsonb")]
    public string? AdditionalPropertiesJson { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Entities.V4;

/// <summary>
/// PostgreSQL entity for physical insulin pump device records
/// Maps to Nocturne.Core.Models.V4.PumpDevice
/// </summary>
[Table("pump_devices")]
[Index(nameof(PumpType), nameof(PumpSerial), IsUnique = true)]
public class PumpDeviceEntity : ITenantScoped
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Primary key - UUID Version 7 for time-ordered, globally unique identification
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Pump type/model name
    /// </summary>
    [Column("pump_type")]
    [MaxLength(128)]
    public string PumpType { get; set; } = string.Empty;

    /// <summary>
    /// Pump serial number
    /// </summary>
    [Column("pump_serial")]
    [MaxLength(128)]
    public string PumpSerial { get; set; } = string.Empty;

    /// <summary>
    /// When this pump was first seen as UTC DateTime (timestamptz)
    /// </summary>
    [Column("first_seen_timestamp")]
    public DateTime FirstSeenTimestamp { get; set; }

    /// <summary>
    /// When this pump was last seen as UTC DateTime (timestamptz)
    /// </summary>
    [Column("last_seen_timestamp")]
    public DateTime LastSeenTimestamp { get; set; }

    /// <summary>
    /// Catch-all JSONB column for fields not mapped to dedicated columns
    /// </summary>
    [Column("additional_properties", TypeName = "jsonb")]
    public string? AdditionalPropertiesJson { get; set; }
}

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Nocturne.Infrastructure.Data.Entities.V4;

/// <summary>
/// PostgreSQL entity for physical insulin pump device records
/// Maps to Nocturne.Core.Models.V4.PumpDevice
/// </summary>
[Table("pump_devices")]
[Index(nameof(PumpType), nameof(PumpSerial), IsUnique = true)]
public class PumpDeviceEntity
{
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
    /// When this pump was first seen in Unix milliseconds
    /// </summary>
    [Column("first_seen_mills")]
    public long FirstSeenMills { get; set; }

    /// <summary>
    /// When this pump was last seen in Unix milliseconds
    /// </summary>
    [Column("last_seen_mills")]
    public long LastSeenMills { get; set; }
}

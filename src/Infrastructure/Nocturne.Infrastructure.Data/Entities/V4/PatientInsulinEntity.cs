using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Entities.V4;

/// <summary>
/// PostgreSQL entity for patient insulin records (rapid-acting, long-acting, etc.)
/// Maps to Nocturne.Core.Models.V4.PatientInsulin
/// </summary>
[Table("patient_insulins")]
public class PatientInsulinEntity : ITenantScoped
{
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>
    /// Primary key - UUID Version 7 for time-ordered, globally unique identification
    /// </summary>
    [Key]
    public Guid Id { get; set; }

    /// <summary>
    /// Category of insulin stored as string (e.g. "RapidActing", "LongActing")
    /// </summary>
    [Column("insulin_category")]
    [MaxLength(32)]
    public string InsulinCategory { get; set; } = string.Empty;

    /// <summary>
    /// Insulin name (e.g. "Humalog", "Lantus")
    /// </summary>
    [Column("name")]
    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Date the insulin was started
    /// </summary>
    [Column("start_date")]
    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// Date the insulin was stopped
    /// </summary>
    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    /// <summary>
    /// Whether this insulin is currently in use
    /// </summary>
    [Column("is_current")]
    public bool IsCurrent { get; set; }

    /// <summary>
    /// Free-text notes about the insulin
    /// </summary>
    [Column("notes")]
    [MaxLength(4096)]
    public string? Notes { get; set; }

    /// <summary>
    /// System tracking: when record was inserted
    /// </summary>
    [Column("sys_created_at")]
    public DateTime SysCreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// System tracking: when record was last updated
    /// </summary>
    [Column("sys_updated_at")]
    public DateTime SysUpdatedAt { get; set; } = DateTime.UtcNow;
}

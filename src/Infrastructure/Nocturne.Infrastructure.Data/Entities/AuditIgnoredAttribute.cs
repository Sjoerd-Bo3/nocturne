namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Properties marked with this attribute are completely omitted from audit log diffs.
/// Use for system-managed fields that always change (SysCreatedAt, SysUpdatedAt, etc.).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AuditIgnoredAttribute : Attribute { }

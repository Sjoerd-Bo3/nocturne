namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Properties marked with this attribute have their values replaced with "[redacted]" in audit log diffs.
/// The fact that a change occurred is recorded, but not the actual values.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class AuditRedactedAttribute : Attribute { }

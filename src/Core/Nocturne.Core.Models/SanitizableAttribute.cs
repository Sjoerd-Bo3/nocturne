namespace Nocturne.Core.Models;

/// <summary>
/// Attribute to mark string properties that should be sanitized for HTML content.
/// Used by <see cref="ProcessableDocumentBase.GetSanitizableFields"/> to discover fields requiring sanitization.
/// </summary>
/// <seealso cref="ProcessableDocumentBase"/>
/// <seealso cref="IProcessableDocument"/>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SanitizableAttribute : Attribute { }

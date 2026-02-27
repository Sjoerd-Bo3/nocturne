namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Marker interface for entities that are scoped to a tenant.
/// Entities implementing this interface will have automatic global query filters
/// and PostgreSQL RLS policies applied.
/// </summary>
public interface ITenantScoped
{
    Guid TenantId { get; set; }
}

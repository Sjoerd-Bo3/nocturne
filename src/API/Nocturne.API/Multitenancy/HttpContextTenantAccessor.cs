using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Multitenancy;

/// <summary>
/// Scoped tenant accessor that stores tenant context for the current request.
/// Set by TenantResolutionMiddleware, read by DbContext and services.
/// </summary>
public class HttpContextTenantAccessor : ITenantAccessor
{
    private TenantContext? _context;

    public Guid TenantId => _context?.TenantId ?? Guid.Empty;
    public bool IsResolved => _context != null;
    public TenantContext? Context => _context;

    public void SetTenant(TenantContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }
}

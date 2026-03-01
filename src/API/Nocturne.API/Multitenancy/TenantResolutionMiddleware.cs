using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Multitenancy;

/// <summary>
/// Middleware that resolves the current tenant from the request subdomain.
/// Must run before AuthenticationMiddleware in the pipeline.
/// </summary>
public class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly MultitenancyConfiguration _config;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IOptions<MultitenancyConfiguration> config,
        IMemoryCache cache)
    {
        _next = next;
        _logger = logger;
        _config = config.Value;
        _cache = cache;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var tenantAccessor = context.RequestServices.GetRequiredService<ITenantAccessor>();
        var slug = ExtractSubdomain(context.Request.Host.Host);

        var tenantContext = await ResolveTenantAsync(context.RequestServices, slug);

        if (tenantContext == null)
        {
            _logger.LogWarning("Tenant not found for slug '{Slug}'", slug);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!tenantContext.IsActive)
        {
            _logger.LogWarning("Tenant '{Slug}' is inactive", slug);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        tenantAccessor.SetTenant(tenantContext);
        context.Items["TenantContext"] = tenantContext;

        await _next(context);
    }

    private string? ExtractSubdomain(string hostname)
    {
        if (string.IsNullOrEmpty(_config.BaseDomain))
            return null;

        // Strip port from BaseDomain for hostname comparison
        // (Host.Host already excludes port, but BaseDomain may include it for frontend URL construction)
        var baseDomainHost = _config.BaseDomain.Split(':')[0];

        if (!hostname.EndsWith($".{baseDomainHost}", StringComparison.OrdinalIgnoreCase))
            return null;

        var subdomain = hostname[..^(baseDomainHost.Length + 1)];
        return string.IsNullOrEmpty(subdomain) ? null : subdomain;
    }

    private async Task<TenantContext?> ResolveTenantAsync(IServiceProvider services, string? slug)
    {
        var cacheKey = $"tenant:{slug ?? "__default__"}";

        if (_cache.TryGetValue(cacheKey, out TenantContext? cached))
            return cached;

        var factory = services.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var context = await factory.CreateDbContextAsync();

        var tenant = slug != null
            ? await context.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Slug == slug)
            : await context.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.IsDefault);

        if (tenant == null)
            return null;

        var tenantContext = new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, tenant.IsActive);
        _cache.Set(cacheKey, tenantContext, CacheDuration);
        return tenantContext;
    }
}

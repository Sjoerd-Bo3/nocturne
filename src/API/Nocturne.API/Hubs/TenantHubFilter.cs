using Microsoft.AspNetCore.SignalR;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Hubs;

/// <summary>
/// SignalR hub filter that ensures the ITenantAccessor is populated for every hub method invocation.
///
/// Each hub method invocation gets a fresh DI scope, so the scoped ITenantAccessor starts empty.
/// This filter reads the TenantContext stored in HttpContext.Items (set by TenantResolutionMiddleware
/// during the initial HTTP upgrade handshake) and sets it on the ITenantAccessor before each method runs.
///
/// This ensures all downstream services (DbContext query filters, services, etc.) have proper tenant context.
/// </summary>
public class TenantHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var httpContext = invocationContext.Context.GetHttpContext();
        var tenantContext = httpContext?.Items[TenantAwareHub.TenantContextKey] as TenantContext;

        if (tenantContext != null)
        {
            var tenantAccessor = httpContext!.RequestServices.GetRequiredService<ITenantAccessor>();
            tenantAccessor.SetTenant(tenantContext);
        }

        return await next(invocationContext);
    }

    public Task OnConnectedAsync(
        HubLifetimeContext context,
        Func<HubLifetimeContext, Task> next)
    {
        // Tenant validation is handled by TenantAwareHub.OnConnectedAsync.
        // We still need to set the accessor here for any OnConnectedAsync logic
        // in derived hubs that uses tenant-scoped services.
        var httpContext = context.Context.GetHttpContext();
        var tenantContext = httpContext?.Items[TenantAwareHub.TenantContextKey] as TenantContext;

        if (tenantContext != null)
        {
            var tenantAccessor = httpContext!.RequestServices.GetRequiredService<ITenantAccessor>();
            tenantAccessor.SetTenant(tenantContext);
        }

        return next(context);
    }

    public Task OnDisconnectedAsync(
        HubLifetimeContext context,
        Exception? exception,
        Func<HubLifetimeContext, Exception?, Task> next)
    {
        var httpContext = context.Context.GetHttpContext();
        var tenantContext = httpContext?.Items[TenantAwareHub.TenantContextKey] as TenantContext;

        if (tenantContext != null)
        {
            var tenantAccessor = httpContext!.RequestServices.GetRequiredService<ITenantAccessor>();
            tenantAccessor.SetTenant(tenantContext);
        }

        return next(context, exception);
    }
}

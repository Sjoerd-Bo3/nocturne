using Microsoft.EntityFrameworkCore;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using OAuthScopes = Nocturne.Core.Models.Authorization.OAuthScopes;
using ScopeTranslator = Nocturne.Core.Models.Authorization.ScopeTranslator;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware that resolves the authenticated user's tenant membership and applies
/// role-based scope restrictions. For followers, scopes are narrowed to the
/// intersection of their granted scopes and the membership's effective scopes.
/// Must run after AuthenticationMiddleware.
/// </summary>
public class MemberScopeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<MemberScopeMiddleware> _logger;

    public MemberScopeMiddleware(RequestDelegate next, ILogger<MemberScopeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authContext = context.GetAuthContext();

        // Only process authenticated users with a resolved tenant
        if (authContext is not { IsAuthenticated: true, SubjectId: not null, TenantId: not null })
        {
            await _next(context);
            return;
        }

        // ApiSecret auth grants admin on the resolved tenant — no membership lookup needed
        if (authContext.AuthType == AuthType.ApiSecret)
        {
            authContext.MemberRole = MembershipScopes.RoleAdmin;
            await _next(context);
            return;
        }

        var dbContext = context.RequestServices.GetRequiredService<NocturneDbContext>();

        var membership = await dbContext.TenantMembers
            .AsNoTracking()
            .Where(tm => tm.SubjectId == authContext.SubjectId.Value
                         && tm.TenantId == authContext.TenantId.Value
                         && tm.RevokedAt == null)
            .Select(tm => new
            {
                tm.Id,
                tm.Role,
                tm.Scopes,
                tm.LimitTo24Hours,
                tm.LastUsedAt
            })
            .FirstOrDefaultAsync();

        if (membership == null)
        {
            // Let the existing AuthenticationMiddleware membership check handle this
            await _next(context);
            return;
        }

        // Set the member role on auth context
        authContext.MemberRole = membership.Role;

        // For followers, restrict scopes and permissions
        if (membership.Role == MembershipScopes.RoleFollower)
        {
            var effectiveScopes = MembershipScopes.Resolve(membership.Role, membership.Scopes);
            var normalizedMemberScopes = OAuthScopes.Normalize(effectiveScopes);

            // Intersect: keep only the user's granted scopes that the membership allows
            var currentScopes = context.GetGrantedScopes();
            var restrictedScopes = normalizedMemberScopes
                .Where(memberScope => OAuthScopes.SatisfiesScope(currentScopes, memberScope))
                .ToHashSet();

            context.Items["GrantedScopes"] = (IReadOnlySet<string>)restrictedScopes;

            // Rebuild permission trie from restricted scopes
            var restrictedPermissions = ScopeTranslator.ToPermissions(restrictedScopes);
            var permissionTrie = new PermissionTrie();
            permissionTrie.Add(restrictedPermissions);
            context.Items["PermissionTrie"] = permissionTrie;

            authContext.LimitTo24Hours = membership.LimitTo24Hours;

            _logger.LogDebug(
                "Follower {SubjectId} on tenant {TenantId} restricted to {ScopeCount} scopes (LimitTo24Hours={LimitTo24Hours})",
                authContext.SubjectId, authContext.TenantId, restrictedScopes.Count, membership.LimitTo24Hours);
        }

        // Fire-and-forget LastUsedAt update (debounced: only if > 5 min since last update)
        if (membership.LastUsedAt == null ||
            (DateTime.UtcNow - membership.LastUsedAt.Value).TotalMinutes > 5)
        {
            var membershipId = membership.Id;
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
            var serviceScopeFactory = context.RequestServices.GetRequiredService<IServiceScopeFactory>();

            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = serviceScopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<NocturneDbContext>();
                    await db.TenantMembers
                        .Where(tm => tm.Id == membershipId)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(tm => tm.LastUsedAt, DateTime.UtcNow)
                            .SetProperty(tm => tm.LastUsedIp, ip)
                            .SetProperty(tm => tm.LastUsedUserAgent, userAgent));
                }
                catch
                {
                    // Best-effort — don't let tracking failures affect the request
                }
            });
        }

        await _next(context);
    }
}

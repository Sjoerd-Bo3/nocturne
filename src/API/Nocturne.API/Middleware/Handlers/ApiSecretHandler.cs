using Microsoft.EntityFrameworkCore;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Middleware.Handlers;

/// <summary>
/// Authentication handler for legacy Nightscout API secret.
/// Validates SHA1 hash of the API secret sent in the api-secret header.
/// Grants full admin (*) permissions.
/// </summary>
public class ApiSecretHandler : IAuthHandler
{
    /// <summary>
    /// Handler priority (400 - last in chain)
    /// </summary>
    public int Priority => 400;

    /// <summary>
    /// Handler name for logging
    /// </summary>
    public string Name => "ApiSecretHandler";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiSecretHandler> _logger;
    private readonly string _apiSecretHash;

    /// <summary>
    /// Creates a new instance of ApiSecretHandler
    /// </summary>
    public ApiSecretHandler(IConfiguration configuration, ILogger<ApiSecretHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Pre-compute the expected hash
        // Check both the new Parameters:api-secret location and legacy API_SECRET for backwards compatibility
        var apiSecret =
            _configuration[$"Parameters:{ServiceNames.Parameters.ApiSecret}"]
            ?? _configuration[ServiceNames.ConfigKeys.ApiSecret]
            ?? "";
        _apiSecretHash = !string.IsNullOrEmpty(apiSecret) ? HashUtils.Sha1Hex(apiSecret) : "";
    }

    /// <inheritdoc />
    public async Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        var apiSecretHeader = context.Request.Headers["api-secret"].FirstOrDefault();
        if (string.IsNullOrEmpty(apiSecretHeader))
            return AuthResult.Skip();

        // Try per-tenant API secret first
        if (context.Items["TenantContext"] is TenantContext tenantCtx)
        {
            var factory = context.RequestServices.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
            await using var dbContext = await factory.CreateDbContextAsync();
            var tenant = await dbContext.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tenantCtx.TenantId);

            if (tenant?.ApiSecretHash != null &&
                string.Equals(apiSecretHeader, tenant.ApiSecretHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug("Per-tenant API secret authentication successful for tenant {Slug}", tenantCtx.Slug);
                return AuthResult.Success(new AuthContext
                {
                    IsAuthenticated = true,
                    AuthType = AuthType.ApiSecret,
                    SubjectName = "admin",
                    Permissions = ["*"],
                    Roles = ["admin"],
                });
            }
        }

        // Fall back to global API secret (backward compat for self-hosted)
        if (string.IsNullOrEmpty(_apiSecretHash))
        {
            _logger.LogWarning("api-secret header provided but no API secret configured");
            return AuthResult.Failure("API secret not configured");
        }

        if (!string.Equals(apiSecretHeader, _apiSecretHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid API secret provided");
            return AuthResult.Failure("Invalid API secret");
        }

        _logger.LogDebug("Global API secret authentication successful");
        return AuthResult.Success(new AuthContext
        {
            IsAuthenticated = true,
            AuthType = AuthType.ApiSecret,
            SubjectName = "admin",
            Permissions = ["*"],
            Roles = ["admin"],
        });
    }
}

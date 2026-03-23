using Nocturne.API.Services.Auth;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware that enforces recovery mode restrictions when active.
/// In recovery mode, only passkey registration/recovery endpoints, metadata,
/// and non-API requests (frontend assets) are allowed through.
/// All other API requests receive a 503 response directing the user to register
/// a passkey to restore normal operation.
/// </summary>
public class RecoveryModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RecoveryModeMiddleware> _logger;

    public RecoveryModeMiddleware(
        RequestDelegate next,
        ILogger<RecoveryModeMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RecoveryModeState state)
    {
        if (!state.IsEnabled)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Allow passkey registration and recovery endpoints
        if (path.StartsWith("/api/auth/passkey/register", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/passkey/recovery", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/passkey/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/metadata", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Block other API endpoints with a clear message
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Recovery mode: blocking request to {Path}", path);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "recovery_mode_active",
                message = "Instance is in recovery mode. Please register a passkey to continue.",
                recoveryMode = true,
            });
            return;
        }

        // Allow non-API requests (frontend assets, health checks, etc.)
        await _next(context);
    }
}

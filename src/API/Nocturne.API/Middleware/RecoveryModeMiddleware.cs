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
        if (!state.IsEnabled && !state.IsSetupRequired)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Allow passkey, TOTP, and metadata endpoints
        if (path.StartsWith("/api/auth/passkey/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/totp/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/metadata", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Block other API endpoints with a clear message
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var mode = state.IsSetupRequired ? "setup" : "recovery";
            _logger.LogDebug("{Mode} mode: blocking request to {Path}", mode, path);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = state.IsSetupRequired ? "setup_required" : "recovery_mode_active",
                message = state.IsSetupRequired
                    ? "Initial setup required. Please register a passkey or authenticator app."
                    : "Instance is in recovery mode. Please register a passkey or authenticator app to continue.",
                setupRequired = state.IsSetupRequired,
                recoveryMode = state.IsEnabled,
            });
            return;
        }

        // Allow non-API requests (frontend assets, health checks, etc.)
        await _next(context);
    }
}

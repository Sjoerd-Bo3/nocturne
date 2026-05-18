
namespace Nocturne.API.Services.Auth;

/// <summary>
/// Hosted service that initialises default authorisation entities on application startup.
/// Creates default roles and the Public system subject if they do not already exist.
/// </summary>
/// <seealso cref="IRoleService"/>
/// <seealso cref="ISubjectService"/>
public class AuthorizationSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuthorizationSeedService> _logger;

    /// <summary>
    /// Initialises a new <see cref="AuthorizationSeedService"/>.
    /// </summary>
    /// <param name="serviceProvider">Root service provider; a new scope is created per invocation.</param>
    /// <param name="logger">Logger instance.</param>
    public AuthorizationSeedService(
        IServiceProvider serviceProvider,
        ILogger<AuthorizationSeedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Seeds default roles via <see cref="IRoleService"/> and the Public system subject via
    /// <see cref="ISubjectService"/>. Errors are logged and do not prevent the application from starting.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var subjectService = scope.ServiceProvider.GetRequiredService<ISubjectService>();

        try
        {
            // Initialize default roles (admin, readable, public, api, careportal, denied)
            var rolesCreated = await roleService.InitializeDefaultRolesAsync();
            if (rolesCreated > 0)
            {
                _logger.LogInformation("Initialized {Count} default role(s)", rolesCreated);
            }

            // Initialize the Public system subject for unauthenticated access
            var publicSubject = await subjectService.InitializePublicSubjectAsync();
            if (publicSubject != null)
            {
                _logger.LogDebug("Public subject initialized: {SubjectId}", publicSubject.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing authorization defaults");
        }
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Singleton that tracks instance auth state at startup.
/// - IsSetupRequired: no non-system subjects exist (fresh install)
/// - IsEnabled: orphaned subjects exist with no passkey/OIDC (post-upgrade)
/// </summary>
public class RecoveryModeState
{
    /// <summary>
    /// Recovery mode: active subjects exist with no auth credentials.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Setup mode: no non-system subjects exist at all (fresh database).
    /// </summary>
    public bool IsSetupRequired { get; set; }
}

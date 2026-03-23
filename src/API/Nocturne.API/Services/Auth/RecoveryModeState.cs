namespace Nocturne.API.Services.Auth;

/// <summary>
/// Singleton that tracks whether the instance is in recovery mode.
/// Recovery mode activates when active non-system subjects exist that have
/// no passkey credentials and no OIDC binding, indicating they were orphaned
/// during an upgrade from legacy Nightscout.
/// </summary>
public class RecoveryModeState
{
    public bool IsEnabled { get; set; }
}

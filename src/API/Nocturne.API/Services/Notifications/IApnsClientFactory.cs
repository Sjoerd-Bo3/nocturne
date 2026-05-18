using dotAPNS;

namespace Nocturne.API.Services.Notifications;

/// <summary>
/// Factory for creating APNs clients, abstracted for testability.
/// The production implementation is <see cref="ApnsClientFactory"/>.
/// </summary>
public interface IApnsClientFactory
{
    /// <summary>
    /// Creates an APNs client for the specified bundle ID.
    /// Returns <see langword="null"/> when APNs configuration is incomplete or when the
    /// underlying client constructor throws.
    /// </summary>
    IApnsClient? CreateClient(string bundleId);

    /// <summary>
    /// <see langword="true"/> when all required APNs configuration values (key, key ID,
    /// 10-character team ID) are present and non-empty.
    /// </summary>
    bool IsConfigured { get; }
}

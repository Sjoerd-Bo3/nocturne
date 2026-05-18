using Nocturne.API.Models;

namespace Nocturne.API.Services.Connectors;

/// <summary>
/// Provides aggregated connector health status for the admin dashboard. See
/// <see cref="ConnectorHealthService"/> for the default implementation.
/// </summary>
public interface IConnectorHealthService
{
    /// <summary>Returns the health status of all configured connectors for the current tenant.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IEnumerable<ConnectorStatusDto>> GetConnectorStatusesAsync(CancellationToken cancellationToken = default);
}

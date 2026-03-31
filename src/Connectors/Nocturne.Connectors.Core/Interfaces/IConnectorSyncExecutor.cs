using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces;

/// <summary>
///     Executes a manual sync for a specific connector.
///     Registered per-connector and discovered by ConnectorSyncService at runtime.
/// </summary>
public interface IConnectorSyncExecutor
{
    /// <summary>
    ///     The connector ID used for dispatch (lowercase, e.g., "dexcom", "librelinkup").
    /// </summary>
    string ConnectorId { get; }

    /// <summary>
    ///     Executes a manual sync using the provided scoped service provider.
    /// </summary>
    Task<SyncResult> ExecuteSyncAsync(
        IServiceProvider scopeProvider,
        SyncRequest request,
        CancellationToken ct,
        ISyncProgressReporter? progressReporter = null);
}

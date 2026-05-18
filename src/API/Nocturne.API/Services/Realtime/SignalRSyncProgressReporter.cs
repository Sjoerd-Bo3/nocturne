using Nocturne.API.Services.Connectors;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.API.Services.Realtime;

/// <summary>
/// <see cref="ISyncProgressReporter"/> implementation that forwards sync progress events
/// to connected clients via <see cref="ISignalRBroadcastService.BroadcastSyncProgressAsync"/>.
/// </summary>
/// <seealso cref="ISyncProgressReporter"/>
/// <seealso cref="ISignalRBroadcastService"/>
/// <seealso cref="ConnectorSyncService"/>
public class SignalRSyncProgressReporter : ISyncProgressReporter
{
    private readonly ISignalRBroadcastService _broadcastService;

    /// <summary>
    /// Initializes a new instance of <see cref="SignalRSyncProgressReporter"/>.
    /// </summary>
    /// <param name="broadcastService">The SignalR broadcast service for progress event delivery.</param>
    public SignalRSyncProgressReporter(ISignalRBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    /// <inheritdoc />
    public Task ReportProgressAsync(SyncProgressEvent progress, CancellationToken ct = default)
    {
        return _broadcastService.BroadcastSyncProgressAsync(progress);
    }
}

using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;

namespace Nocturne.API.Services;

public class SignalRSyncProgressReporter : ISyncProgressReporter
{
    private readonly ISignalRBroadcastService _broadcastService;

    public SignalRSyncProgressReporter(ISignalRBroadcastService broadcastService)
    {
        _broadcastService = broadcastService;
    }

    public Task ReportProgressAsync(SyncProgressEvent progress, CancellationToken ct = default)
    {
        return _broadcastService.BroadcastSyncProgressAsync(progress);
    }
}

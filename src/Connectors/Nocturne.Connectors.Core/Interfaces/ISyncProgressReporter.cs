using Nocturne.Connectors.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces;

public interface ISyncProgressReporter
{
    Task ReportProgressAsync(SyncProgressEvent progress, CancellationToken ct = default);
}

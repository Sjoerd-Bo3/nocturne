using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces;

public interface IGlucosePublisher
{
    Task<bool> PublishEntriesAsync(
        IEnumerable<Entry> entries,
        string source,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetLatestEntryTimestampAsync(
        string source,
        CancellationToken cancellationToken = default);
}

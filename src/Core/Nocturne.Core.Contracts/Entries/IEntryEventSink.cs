using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Entries;

/// <summary>
/// Driven port for entry write-event propagation. The adapter translates
/// these into SignalR broadcasts and cache invalidation. Failures are non-fatal.
/// </summary>
public interface IEntryEventSink
{
    Task OnCreatedAsync(IReadOnlyList<Entry> entries, CancellationToken ct = default);
    Task OnUpdatedAsync(Entry entry, CancellationToken ct = default);
    Task BeforeDeleteAsync(string id, CancellationToken ct = default);
    Task OnDeletedAsync(Entry? entry, CancellationToken ct = default);
    Task OnBulkDeletedAsync(long deletedCount, CancellationToken ct = default);
}

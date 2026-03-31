namespace Nocturne.Core.Contracts.Events;

/// <summary>
/// Generic driven port for data write-event propagation.
/// Adapters translate these into SignalR broadcasts, cache invalidation,
/// write-back to external systems, etc. Failures are non-fatal.
/// </summary>
public interface IDataEventSink<in T>
{
    Task OnCreatedAsync(IReadOnlyList<T> items, CancellationToken ct = default) => Task.CompletedTask;
    Task OnCreatedAsync(T item, CancellationToken ct = default) => Task.CompletedTask;
    Task OnUpdatedAsync(T item, CancellationToken ct = default) => Task.CompletedTask;
    Task BeforeDeleteAsync(string id, CancellationToken ct = default) => Task.CompletedTask;
    Task OnDeletedAsync(T? item, CancellationToken ct = default) => Task.CompletedTask;
    Task OnBulkDeletedAsync(long deletedCount, CancellationToken ct = default) => Task.CompletedTask;
}

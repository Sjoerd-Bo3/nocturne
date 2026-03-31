using Microsoft.Extensions.Logging;

namespace Nocturne.Core.Contracts.Events;

/// <summary>
/// Fans out every <see cref="IDataEventSink{T}"/> call to all registered sinks.
/// Each sink is invoked independently so that one failure cannot block others.
/// </summary>
public class CompositeDataEventSink<T>(
    IEnumerable<IDataEventSink<T>> sinks,
    ILogger<CompositeDataEventSink<T>>? logger = null) : IDataEventSink<T>
{
    private readonly IReadOnlyList<IDataEventSink<T>> _sinks = sinks.ToList();

    public async Task OnCreatedAsync(IReadOnlyList<T> items, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.OnCreatedAsync(items, ct));
    }

    public async Task OnCreatedAsync(T item, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.OnCreatedAsync(item, ct));
    }

    public async Task OnUpdatedAsync(T item, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.OnUpdatedAsync(item, ct));
    }

    public async Task BeforeDeleteAsync(string id, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.BeforeDeleteAsync(id, ct));
    }

    public async Task OnDeletedAsync(T? item, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.OnDeletedAsync(item, ct));
    }

    public async Task OnBulkDeletedAsync(long deletedCount, CancellationToken ct = default)
    {
        foreach (var sink in _sinks)
            await InvokeAsync(sink, s => s.OnBulkDeletedAsync(deletedCount, ct));
    }

    private async Task InvokeAsync(IDataEventSink<T> sink, Func<IDataEventSink<T>, Task> action)
    {
        try
        {
            await action(sink);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Data event sink {SinkType} failed", sink.GetType().Name);
        }
    }
}

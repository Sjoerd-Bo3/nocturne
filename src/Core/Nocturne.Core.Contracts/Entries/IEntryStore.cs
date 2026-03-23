using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Entries;

/// <summary>
/// Driven port for all entry persistence. Abstracts dual-path storage
/// (legacy entries table + V4 projected entries) behind a single interface.
/// The adapter handles write routing, read-time merging, and projection.
/// </summary>
public interface IEntryStore
{
    Task<IReadOnlyList<Entry>> QueryAsync(EntryQuery query, CancellationToken ct = default);
    Task<Entry?> GetCurrentAsync(CancellationToken ct = default);
    Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Entry?> CheckDuplicateAsync(string? device, string type, double? sgv, long mills,
        int windowMinutes = 5, CancellationToken ct = default);
    Task<IReadOnlyList<Entry>> CreateAsync(IEnumerable<Entry> entries, CancellationToken ct = default);
    Task<Entry?> UpdateAsync(string id, Entry entry, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<long> BulkDeleteAsync(string? find, CancellationToken ct = default);
}

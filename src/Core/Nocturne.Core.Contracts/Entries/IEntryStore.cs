using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Entries;

/// <summary>
/// Driven port for entry reads. Abstracts dual-path storage
/// (legacy entries table + V4 projected entries) behind a single interface.
/// The adapter handles read-time merging, projection, and deduplication.
/// </summary>
public interface IEntryStore
{
    Task<IReadOnlyList<Entry>> QueryAsync(EntryQuery query, CancellationToken ct = default);
    Task<Entry?> GetCurrentAsync(CancellationToken ct = default);
    Task<Entry?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<Entry?> CheckDuplicateAsync(string? device, string type, double? sgv, long mills,
        int windowMinutes = 5, CancellationToken ct = default);
}

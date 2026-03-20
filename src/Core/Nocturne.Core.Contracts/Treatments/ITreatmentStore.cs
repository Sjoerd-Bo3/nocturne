using System.Text.Json;
using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Driven port for all treatment persistence. Abstracts dual-path storage
/// (legacy treatments table + V4 granular tables) behind a single interface.
/// The adapter handles write routing, read-time merging, decomposition, and projection.
/// </summary>
public interface ITreatmentStore
{
    Task<IReadOnlyList<Treatment>> QueryAsync(TreatmentQuery query, CancellationToken ct = default);
    Task<Treatment?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Treatment>> GetModifiedSinceAsync(long lastModifiedMills, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Treatment>> CreateAsync(IReadOnlyList<Treatment> treatments, CancellationToken ct = default);
    Task<Treatment?> UpdateAsync(string id, Treatment treatment, CancellationToken ct = default);
    Task<Treatment?> PatchAsync(string id, JsonElement patchData, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<long> BulkDeleteAsync(string? find, CancellationToken ct = default);
}

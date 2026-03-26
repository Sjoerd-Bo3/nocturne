using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Driven port for treatment persistence. Abstracts dual-path storage
/// (legacy treatments table + V4 granular tables) behind a single interface.
/// The adapter handles write routing for operations that need dual-path awareness
/// (create, update, delete), plus read-time merging, decomposition, and projection.
/// Pure pass-through writes (patch, bulk delete) go directly to ITreatmentRepository.
/// </summary>
public interface ITreatmentStore
{
    Task<IReadOnlyList<Treatment>> QueryAsync(TreatmentQuery query, CancellationToken ct = default);
    Task<Treatment?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Treatment>> GetModifiedSinceAsync(long lastModifiedMills, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<Treatment>> CreateAsync(IReadOnlyList<Treatment> treatments, CancellationToken ct = default);
    Task<Treatment?> UpdateAsync(string id, Treatment treatment, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

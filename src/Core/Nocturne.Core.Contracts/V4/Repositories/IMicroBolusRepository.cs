using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IMicroBolusRepository
{
    Task<IEnumerable<MicroBolus>> GetAsync(
        long? from,
        long? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<MicroBolus?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<MicroBolus?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<MicroBolus> CreateAsync(MicroBolus model, CancellationToken ct = default);
    Task<MicroBolus> UpdateAsync(Guid id, MicroBolus model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(long? from, long? to, CancellationToken ct = default);
    Task<IEnumerable<MicroBolus>> BulkCreateAsync(
        IEnumerable<MicroBolus> records,
        CancellationToken ct = default
    );
}

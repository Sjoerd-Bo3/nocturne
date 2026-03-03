using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ITempBasalRepository
{
    Task<IEnumerable<TempBasal>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<TempBasal?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TempBasal?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<TempBasal> CreateAsync(TempBasal model, CancellationToken ct = default);
    Task<TempBasal> UpdateAsync(Guid id, TempBasal model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<TempBasal>> BulkCreateAsync(
        IEnumerable<TempBasal> records,
        CancellationToken ct = default
    );
    Task<int> DeleteBySourceAndDateRangeAsync(
        string source,
        DateTime from,
        DateTime to,
        CancellationToken ct = default
    );
}

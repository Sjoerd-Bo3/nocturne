using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IBGCheckRepository : IV4Repository<BGCheck>
{
    Task<IEnumerable<BGCheck>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        bool nativeOnly = false,
        CancellationToken ct = default
    );

    // Explicit base-interface bridge — delegates to the extended overload
    Task<IEnumerable<BGCheck>> IV4Repository<BGCheck>.GetAsync(
        DateTime? from, DateTime? to, string? device, string? source,
        int limit, int offset, bool descending, CancellationToken ct)
        => GetAsync(from, to, device, source, limit, offset, descending, false, ct);
    Task<BGCheck?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BGCheck?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<BGCheck> CreateAsync(BGCheck model, CancellationToken ct = default);
    Task<BGCheck> UpdateAsync(Guid id, BGCheck model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<BGCheck>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<BGCheck>> BulkCreateAsync(
        IEnumerable<BGCheck> records,
        CancellationToken ct = default
    );
}

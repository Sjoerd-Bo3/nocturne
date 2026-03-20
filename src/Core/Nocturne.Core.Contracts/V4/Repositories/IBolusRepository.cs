using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IBolusRepository : IV4Repository<Bolus>
{
    Task<IEnumerable<Bolus>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        bool nativeOnly = false,
        BolusKind? kind = null,
        CancellationToken ct = default
    );

    // Explicit base-interface bridge — delegates to the extended overload
    Task<IEnumerable<Bolus>> IV4Repository<Bolus>.GetAsync(
        DateTime? from, DateTime? to, string? device, string? source,
        int limit, int offset, bool descending, CancellationToken ct)
        => GetAsync(from, to, device, source, limit, offset, descending, false, null, ct);
    Task<Bolus?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Bolus?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<Bolus> CreateAsync(Bolus model, CancellationToken ct = default);
    Task<Bolus> UpdateAsync(Guid id, Bolus model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<Bolus>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<Bolus>> BulkCreateAsync(
        IEnumerable<Bolus> records,
        CancellationToken ct = default
    );
}

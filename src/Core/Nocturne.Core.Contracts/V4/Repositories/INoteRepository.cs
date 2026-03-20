using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface INoteRepository : IV4Repository<Note>
{
    Task<IEnumerable<Note>> GetAsync(
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
    Task<IEnumerable<Note>> IV4Repository<Note>.GetAsync(
        DateTime? from, DateTime? to, string? device, string? source,
        int limit, int offset, bool descending, CancellationToken ct)
        => GetAsync(from, to, device, source, limit, offset, descending, false, ct);
    Task<Note?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Note?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<Note> CreateAsync(Note model, CancellationToken ct = default);
    Task<Note> UpdateAsync(Guid id, Note model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<Note>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<Note>> BulkCreateAsync(
        IEnumerable<Note> records,
        CancellationToken ct = default
    );
}

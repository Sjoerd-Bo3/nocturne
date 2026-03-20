using System.Text.Json;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

public class TreatmentService : ITreatmentService
{
    private readonly ITreatmentStore _store;
    private readonly ITreatmentCache _cache;
    private readonly ITreatmentEventSink _events;
    private readonly ILogger<TreatmentService> _logger;

    public TreatmentService(
        ITreatmentStore store,
        ITreatmentCache cache,
        ITreatmentEventSink events,
        ILogger<TreatmentService> logger)
    {
        _store = store;
        _cache = cache;
        _events = events;
        _logger = logger;
    }

    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        string? find = null, int? count = null, int? skip = null,
        CancellationToken cancellationToken = default)
    {
        var query = new TreatmentQuery
        {
            Find = find,
            Count = count ?? 10,
            Skip = skip ?? 0
        };

        var cached = await _cache.GetOrComputeAsync(
            query,
            () => _store.QueryAsync(query, cancellationToken),
            cancellationToken);

        return cached ?? await _store.QueryAsync(query, cancellationToken);
    }

    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count, int skip = 0, CancellationToken cancellationToken = default)
    {
        return await GetTreatmentsAsync(null, count, skip, cancellationToken);
    }

    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return await _store.GetByIdAsync(id, cancellationToken);
    }

    public async Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count, int skip, string? findQuery, bool reverseResults,
        CancellationToken cancellationToken = default)
    {
        var query = new TreatmentQuery
        {
            Find = findQuery,
            Count = count,
            Skip = skip,
            ReverseResults = reverseResults
        };

        return await _store.QueryAsync(query, cancellationToken);
    }

    public async Task<IEnumerable<Treatment>> GetTreatmentsModifiedSinceAsync(
        long lastModifiedMills, int limit = 500, CancellationToken cancellationToken = default)
    {
        return await _store.GetModifiedSinceAsync(lastModifiedMills, limit, cancellationToken);
    }

    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments, CancellationToken cancellationToken = default)
    {
        var created = await _store.CreateAsync(treatments.ToList(), cancellationToken);

        await _cache.InvalidateAsync(cancellationToken);
        await _events.OnCreatedAsync(created, cancellationToken);

        return created;
    }

    public async Task<Treatment?> UpdateTreatmentAsync(
        string id, Treatment treatment, CancellationToken cancellationToken = default)
    {
        var updated = await _store.UpdateAsync(id, treatment, cancellationToken);
        if (updated is null) return null;

        await _cache.InvalidateAsync(cancellationToken);
        await _events.OnUpdatedAsync(updated, cancellationToken);

        return updated;
    }

    public async Task<Treatment?> PatchTreatmentAsync(
        string id, JsonElement patchData, CancellationToken cancellationToken = default)
    {
        var patched = await _store.PatchAsync(id, patchData, cancellationToken);
        if (patched is null) return null;

        await _cache.InvalidateAsync(cancellationToken);
        await _events.OnUpdatedAsync(patched, cancellationToken);

        return patched;
    }

    public async Task<bool> DeleteTreatmentAsync(
        string id, CancellationToken cancellationToken = default)
    {
        var existing = await _store.GetByIdAsync(id, cancellationToken);
        var deleted = await _store.DeleteAsync(id, cancellationToken);

        if (deleted)
        {
            await _cache.InvalidateAsync(cancellationToken);
            if (existing is not null)
                await _events.OnDeletedAsync(existing, cancellationToken);
        }

        return deleted;
    }

    public async Task<long> DeleteTreatmentsAsync(
        string? find = null, CancellationToken cancellationToken = default)
    {
        var count = await _store.BulkDeleteAsync(find, cancellationToken);
        if (count > 0)
            await _cache.InvalidateAsync(cancellationToken);
        return count;
    }
}

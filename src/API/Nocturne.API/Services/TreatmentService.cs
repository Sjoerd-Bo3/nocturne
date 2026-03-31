using System.Text.Json;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services;

public class TreatmentService : ITreatmentService
{
    private readonly ITreatmentStore _store;
    private readonly ITreatmentRepository _repository;
    private readonly ITreatmentCache _cache;
    private readonly IDataEventSink<Treatment> _events;
    private readonly IPatientInsulinRepository _insulinRepo;
    private readonly ILogger<TreatmentService> _logger;

    private static readonly HashSet<string> BolusEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Snack Bolus",
        "Meal Bolus",
        "Correction Bolus",
        "Combo Bolus"
    };

    private static readonly HashSet<string> BasalEventTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Temp Basal",
        "Temp Basal Start"
    };

    public TreatmentService(
        ITreatmentStore store,
        ITreatmentRepository repository,
        ITreatmentCache cache,
        IDataEventSink<Treatment> events,
        IPatientInsulinRepository insulinRepo,
        ILogger<TreatmentService> logger)
    {
        _store = store;
        _repository = repository;
        _cache = cache;
        _events = events;
        _insulinRepo = insulinRepo;
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
        var treatmentList = treatments.ToList();

        await PopulateInsulinContextAsync(treatmentList, cancellationToken);

        var created = await _store.CreateAsync(treatmentList, cancellationToken);

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
        var patched = await _repository.PatchTreatmentAsync(id, patchData, cancellationToken);
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
        var count = await _repository.BulkDeleteTreatmentsAsync(find ?? "{}", cancellationToken);
        if (count > 0)
            await _cache.InvalidateAsync(cancellationToken);
        return count;
    }

    private async Task PopulateInsulinContextAsync(
        List<Treatment> treatments, CancellationToken cancellationToken)
    {
        // Determine which lookups we need so we only hit the repo once per type
        var needsBolus = treatments.Any(t =>
            t.InsulinContext is null && t.EventType is not null && BolusEventTypes.Contains(t.EventType));
        var needsBasal = treatments.Any(t =>
            t.InsulinContext is null && t.EventType is not null && BasalEventTypes.Contains(t.EventType));

        PatientInsulin? bolusInsulin = null;
        PatientInsulin? basalInsulin = null;

        if (needsBolus)
            bolusInsulin = await _insulinRepo.GetPrimaryBolusInsulinAsync(cancellationToken);
        if (needsBasal)
            basalInsulin = await _insulinRepo.GetPrimaryBasalInsulinAsync(cancellationToken);

        foreach (var treatment in treatments)
        {
            if (treatment.InsulinContext is not null || treatment.EventType is null)
                continue;

            PatientInsulin? insulin = null;
            if (BolusEventTypes.Contains(treatment.EventType))
                insulin = bolusInsulin;
            else if (BasalEventTypes.Contains(treatment.EventType))
                insulin = basalInsulin;

            if (insulin is not null)
            {
                treatment.InsulinContext = new TreatmentInsulinContext
                {
                    PatientInsulinId = insulin.Id,
                    InsulinName = insulin.Name,
                    Dia = insulin.Dia,
                    Peak = insulin.Peak,
                    Curve = insulin.Curve,
                    Concentration = insulin.Concentration
                };
            }
        }
    }
}

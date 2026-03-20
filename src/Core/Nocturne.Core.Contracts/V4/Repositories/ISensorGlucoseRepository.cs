using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ISensorGlucoseRepository : IV4Repository<SensorGlucose>
{
    Task<IEnumerable<SensorGlucose>> GetAsync(
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
    Task<IEnumerable<SensorGlucose>> IV4Repository<SensorGlucose>.GetAsync(
        DateTime? from, DateTime? to, string? device, string? source,
        int limit, int offset, bool descending, CancellationToken ct)
        => GetAsync(from, to, device, source, limit, offset, descending, false, ct);
    Task<SensorGlucose?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SensorGlucose?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<SensorGlucose> CreateAsync(SensorGlucose model, CancellationToken ct = default);
    Task<SensorGlucose> UpdateAsync(Guid id, SensorGlucose model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<SensorGlucose>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<SensorGlucose>> BulkCreateAsync(
        IEnumerable<SensorGlucose> records,
        CancellationToken ct = default
    );
    Task<DateTime?> GetLatestTimestampAsync(string? source = null, CancellationToken ct = default);
}

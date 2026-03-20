using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IDeviceEventRepository : IV4Repository<DeviceEvent>
{
    Task<IEnumerable<DeviceEvent>> GetAsync(
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
    Task<IEnumerable<DeviceEvent>> IV4Repository<DeviceEvent>.GetAsync(
        DateTime? from, DateTime? to, string? device, string? source,
        int limit, int offset, bool descending, CancellationToken ct)
        => GetAsync(from, to, device, source, limit, offset, descending, false, ct);
    Task<DeviceEvent?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<DeviceEvent?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<DeviceEvent> CreateAsync(DeviceEvent model, CancellationToken ct = default);
    Task<DeviceEvent> UpdateAsync(Guid id, DeviceEvent model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<DeviceEvent>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<DeviceEvent>> BulkCreateAsync(
        IEnumerable<DeviceEvent> records,
        CancellationToken ct = default
    );
    Task<DeviceEvent?> GetLatestByEventTypeAsync(DeviceEventType eventType, CancellationToken ct = default);
    Task<DeviceEvent?> GetLatestByEventTypesAsync(DeviceEventType[] eventTypes, CancellationToken ct = default);
}

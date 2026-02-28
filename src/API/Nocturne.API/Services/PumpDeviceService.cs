using System.Collections.Concurrent;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services;

public class PumpDeviceService : IPumpDeviceService
{
    private readonly IPumpDeviceRepository _repository;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ConcurrentDictionary<(string, string, string), Guid> _cache = new();

    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public PumpDeviceService(IPumpDeviceRepository repository, ITenantAccessor tenantAccessor)
    {
        _repository = repository;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<Guid?> ResolveAsync(string? pumpType, string? pumpSerial, long mills, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pumpType) || string.IsNullOrEmpty(pumpSerial))
            return null;

        var tenantId = TenantCacheId;
        var key = (tenantId, pumpType, pumpSerial);
        if (_cache.TryGetValue(key, out var cachedId))
            return cachedId;

        var existing = await _repository.FindByTypeAndSerialAsync(pumpType, pumpSerial, ct);
        var timestamp = DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime;
        if (existing is not null)
        {
            if (timestamp > existing.LastSeenTimestamp)
            {
                existing.LastSeenTimestamp = timestamp;
                await _repository.UpdateAsync(existing.Id, existing, ct);
            }
            _cache[key] = existing.Id;
            return existing.Id;
        }

        var device = new PumpDevice
        {
            Id = Guid.CreateVersion7(),
            PumpType = pumpType,
            PumpSerial = pumpSerial,
            FirstSeenTimestamp = timestamp,
            LastSeenTimestamp = timestamp
        };
        var created = await _repository.CreateAsync(device, ct);
        _cache[key] = created.Id;
        return created.Id;
    }
}

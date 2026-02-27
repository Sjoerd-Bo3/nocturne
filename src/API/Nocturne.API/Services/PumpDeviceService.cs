using System.Collections.Concurrent;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services;

public class PumpDeviceService : IPumpDeviceService
{
    private readonly IPumpDeviceRepository _repository;
    private readonly ConcurrentDictionary<(string, string), Guid> _cache = new();

    public PumpDeviceService(IPumpDeviceRepository repository)
    {
        _repository = repository;
    }

    public async Task<Guid?> ResolveAsync(string? pumpType, string? pumpSerial, long mills, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(pumpType) || string.IsNullOrEmpty(pumpSerial))
            return null;

        var key = (pumpType, pumpSerial);
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

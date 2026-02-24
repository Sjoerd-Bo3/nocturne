namespace Nocturne.Core.Contracts;

public interface IPumpDeviceService
{
    Task<Guid?> ResolveAsync(string? pumpType, string? pumpSerial, long mills, CancellationToken ct = default);
}

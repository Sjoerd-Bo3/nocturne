using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for device status operations with WebSocket broadcasting
/// </summary>
public class DeviceStatusService : IDeviceStatusService
{
    private readonly IDeviceStatusRepository _deviceStatuses;
    private readonly IWriteSideEffects _sideEffects;
    private readonly ICacheService _cacheService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<DeviceStatusService> _logger;
    private const string CollectionName = "devicestatus";

    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public DeviceStatusService(
        IDeviceStatusRepository deviceStatuses,
        IWriteSideEffects sideEffects,
        ICacheService cacheService,
        ITenantAccessor tenantAccessor,
        ILogger<DeviceStatusService> logger
    )
    {
        _deviceStatuses = deviceStatuses;
        _sideEffects = sideEffects;
        _cacheService = cacheService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        // Cache device status only for the common case of recent entries with default pagination
        if (string.IsNullOrEmpty(find) && (count ?? 10) == 10 && (skip ?? 0) == 0)
        {
            var cacheKey = $"devicestatus:current:{TenantCacheId}";
            var cacheTtl = TimeSpan.FromSeconds(60);

            var cachedDeviceStatus = await _cacheService.GetAsync<IEnumerable<DeviceStatus>>(
                cacheKey,
                cancellationToken
            );
            if (cachedDeviceStatus != null)
            {
                _logger.LogDebug("Cache HIT for current device status");
                return cachedDeviceStatus;
            }

            _logger.LogDebug("Cache MISS for current device status, fetching from database");
            var deviceStatus = await _deviceStatuses.GetDeviceStatusAsync(
                10,
                0,
                cancellationToken
            );

            if (deviceStatus != null)
            {
                await _cacheService.SetAsync(cacheKey, deviceStatus, cacheTtl, cancellationToken);
                _logger.LogDebug(
                    "Cached current device status with {TTL}s TTL",
                    cacheTtl.TotalSeconds
                );
            }

            return deviceStatus ?? new List<DeviceStatus>();
        }

        // For non-default parameters, go directly to database
        _logger.LogDebug(
            "Bypassing cache for device status with custom parameters (find: {Find}, count: {Count}, skip: {Skip})",
            find,
            count,
            skip
        );
        return await _deviceStatuses.GetDeviceStatusWithAdvancedFilterAsync(
            count: count ?? 10,
            skip: skip ?? 0,
            findQuery: find,
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _deviceStatuses.GetDeviceStatusByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatusEntries,
        CancellationToken cancellationToken = default
    )
    {
        var createdDeviceStatus = await _deviceStatuses.CreateDeviceStatusAsync(
            deviceStatusEntries,
            cancellationToken
        );

        await _sideEffects.OnCreatedAsync(
            CollectionName,
            createdDeviceStatus.ToList(),
            cancellationToken: cancellationToken
        );

        return createdDeviceStatus;
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        var updatedDeviceStatus = await _deviceStatuses.UpdateDeviceStatusAsync(
            id,
            deviceStatus,
            cancellationToken
        );

        if (updatedDeviceStatus != null)
        {
            await _sideEffects.OnUpdatedAsync(
                CollectionName,
                updatedDeviceStatus,
                cancellationToken: cancellationToken
            );
        }

        return updatedDeviceStatus;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDeviceStatusAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        await _sideEffects.BeforeDeleteAsync<DeviceStatus>(
            id,
            new WriteEffectOptions { DecomposeToV4 = true },
            cancellationToken
        );

        // Get the device status before deleting for broadcasting
        var deviceStatusToDelete = await _deviceStatuses.GetDeviceStatusByIdAsync(
            id,
            cancellationToken
        );

        var deleted = await _deviceStatuses.DeleteDeviceStatusAsync(id, cancellationToken);

        if (deleted)
        {
            await _sideEffects.OnDeletedAsync(
                CollectionName,
                deviceStatusToDelete,
                cancellationToken: cancellationToken
            );
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteDeviceStatusEntriesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        var deletedCount = await _deviceStatuses.BulkDeleteDeviceStatusAsync(
            find ?? "{}",
            cancellationToken
        );

        await _sideEffects.OnBulkDeletedAsync(
            CollectionName,
            deletedCount,
            cancellationToken: cancellationToken
        );

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> GetRecentDeviceStatusAsync(
        int count = 10,
        CancellationToken cancellationToken = default
    )
    {
        return await _deviceStatuses.GetDeviceStatusAsync(count, 0, cancellationToken);
    }
}

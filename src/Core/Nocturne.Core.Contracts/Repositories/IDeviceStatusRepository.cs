using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for DeviceStatus domain operations
/// </summary>
public interface IDeviceStatusRepository
{
    /// <summary>
    /// Get device status entries with pagination
    /// </summary>
    /// <param name="count">Maximum number of device status entries to return</param>
    /// <param name="skip">Number of device status entries to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of device status entries</returns>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a device status entry by its ID
    /// </summary>
    /// <param name="id">The device status ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The device status entry if found, null otherwise</returns>
    Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get device status entries with advanced filtering options
    /// </summary>
    /// <param name="count">Maximum number of device status entries to return</param>
    /// <param name="skip">Number of device status entries to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of device status entries matching the filter</returns>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create multiple device status entries
    /// </summary>
    /// <param name="deviceStatuses">The device status entries to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created device status entries</returns>
    Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update a device status entry by ID
    /// </summary>
    /// <param name="id">The device status ID</param>
    /// <param name="deviceStatus">The updated device status data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated device status entry, or null if not found</returns>
    Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a device status entry by ID
    /// </summary>
    /// <param name="id">The device status ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteDeviceStatusAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete device status entries using query filters
    /// </summary>
    /// <param name="findQuery">Query filter for device status entries to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of device status entries deleted</returns>
    Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count device status entries with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of device status entries matching the filter</returns>
    Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get device status records modified since a given timestamp (for incremental sync)
    /// </summary>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    );
}

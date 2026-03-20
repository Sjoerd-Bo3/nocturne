using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service to provide device age information using the V4 DeviceEvents system
/// </summary>
public interface IDeviceAgeService
{
    /// <summary>
    /// Get cannula/site age (CAGE)
    /// </summary>
    Task<DeviceAgeInfo> GetCannulaAgeAsync(DeviceAgePreferences prefs, CancellationToken ct = default);

    /// <summary>
    /// Get sensor age (SAGE) - returns composite with both start and change events
    /// </summary>
    Task<SensorAgeInfo> GetSensorAgeAsync(DeviceAgePreferences prefs, CancellationToken ct = default);

    /// <summary>
    /// Get insulin reservoir age (IAGE)
    /// </summary>
    Task<DeviceAgeInfo> GetInsulinAgeAsync(DeviceAgePreferences prefs, CancellationToken ct = default);

    /// <summary>
    /// Get pump battery age (BAGE)
    /// </summary>
    Task<DeviceAgeInfo> GetBatteryAgeAsync(DeviceAgePreferences prefs, CancellationToken ct = default);
}

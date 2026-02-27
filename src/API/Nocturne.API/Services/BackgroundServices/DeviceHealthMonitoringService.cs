using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service for continuous device health monitoring and maintenance alerts
/// </summary>
public class DeviceHealthMonitoringService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeviceHealthMonitoringService> _logger;
    private readonly DeviceHealthOptions _options;

    /// <summary>
    /// Initializes a new instance of the DeviceHealthMonitoringService
    /// </summary>
    /// <param name="serviceProvider">Service provider for creating scoped services</param>
    /// <param name="logger">Logger</param>
    /// <param name="options">Device health options</param>
    public DeviceHealthMonitoringService(
        IServiceProvider serviceProvider,
        ILogger<DeviceHealthMonitoringService> logger,
        IOptions<DeviceHealthOptions> options
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Execute the background monitoring service
    /// </summary>
    /// <param name="stoppingToken">Stopping token</param>
    /// <returns>Task completion</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Device Health Monitoring Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PerformHealthCheckAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during device health monitoring");
            }

            // Wait for the next health check interval
            try
            {
                await Task.Delay(
                    TimeSpan.FromMinutes(_options.HealthCheckIntervalMinutes),
                    stoppingToken
                );
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Device Health Monitoring Service stopped");
    }

    /// <summary>
    /// Perform health check across all active tenants
    /// </summary>
    private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
    {
        // Lookup active tenants using unfiltered context
        using var lookupScope = _serviceProvider.CreateScope();
        var factory = lookupScope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var lookupContext = await factory.CreateDbContextAsync(cancellationToken);
        var tenants = await lookupContext.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Slug, t.DisplayName })
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
                tenantAccessor.SetTenant(new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, true));

                await PerformHealthCheckForTenantAsync(scope.ServiceProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during device health monitoring for tenant {TenantSlug}", tenant.Slug);
            }
        }
    }

    /// <summary>
    /// Perform health check on all registered devices for a single tenant
    /// </summary>
    private async Task PerformHealthCheckForTenantAsync(IServiceProvider scopedProvider, CancellationToken cancellationToken)
    {
        var dbContext = scopedProvider.GetRequiredService<NocturneDbContext>();
        var deviceRegistry = scopedProvider.GetRequiredService<IDeviceRegistryService>();
        var alertEngine = scopedProvider.GetRequiredService<IDeviceAlertEngine>();
        var trackerSuggestionService = scopedProvider.GetRequiredService<ITrackerSuggestionService>();

        _logger.LogDebug("Starting device health check cycle");

        try
        {
            // Get all active devices
            var devices = await GetActiveDevicesAsync(dbContext, cancellationToken);

            _logger.LogDebug("Checking health for {DeviceCount} devices", devices.Count);

            // Process devices in batches to avoid overwhelming the system
            var batchSize = 10;
            for (int i = 0; i < devices.Count; i += batchSize)
            {
                var batch = devices.Skip(i).Take(batchSize).ToList();
                await ProcessDeviceBatchAsync(
                    batch,
                    deviceRegistry,
                    alertEngine,
                    trackerSuggestionService,
                    cancellationToken
                );
            }

            _logger.LogDebug(
                "Completed device health check cycle for {DeviceCount} devices",
                devices.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during health check cycle");
            throw;
        }
    }

    /// <summary>
    /// Get all active devices that need health monitoring
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device IDs to check</returns>
    private async Task<List<string>> GetActiveDevicesAsync(
        NocturneDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        return await dbContext
            .DeviceHealth.Where(d =>
                d.Status == DeviceStatusType.Active
                || d.Status == DeviceStatusType.Warning
                || d.Status == DeviceStatusType.Maintenance
            )
            .Select(d => d.DeviceId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Process a batch of devices for health monitoring
    /// </summary>
    private async Task ProcessDeviceBatchAsync(
        List<string> deviceIds,
        IDeviceRegistryService deviceRegistry,
        IDeviceAlertEngine alertEngine,
        ITrackerSuggestionService trackerSuggestionService,
        CancellationToken cancellationToken
    )
    {
        var tasks = deviceIds.Select(deviceId =>
            ProcessSingleDeviceAsync(
                deviceId,
                deviceRegistry,
                alertEngine,
                trackerSuggestionService,
                cancellationToken
            )
        );

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Process health monitoring for a single device
    /// </summary>
    private async Task ProcessSingleDeviceAsync(
        string deviceId,
        IDeviceRegistryService deviceRegistry,
        IDeviceAlertEngine alertEngine,
        ITrackerSuggestionService trackerSuggestionService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            if (_options.EnableDebugLogging)
            {
                _logger.LogDebug("Processing device health for device {DeviceId}", deviceId);
            }

            // Get device information
            var device = await deviceRegistry.GetDeviceAsync(deviceId, cancellationToken);
            if (device == null)
            {
                _logger.LogWarning("Device {DeviceId} not found during health check", deviceId);
                return;
            }

            // Generate and process alerts
            var alerts = await alertEngine.ProcessDeviceAlertsAsync(device, cancellationToken);

            // Send any generated alerts
            foreach (var alert in alerts)
            {
                try
                {
                    await alertEngine.SendDeviceAlertAsync(alert, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send alert {AlertType} for device {DeviceId}",
                        alert.AlertType,
                        deviceId
                    );
                }
            }

            // Perform device-specific monitoring
            await PerformDeviceSpecificMonitoringAsync(device, trackerSuggestionService, cancellationToken);

            if (_options.EnableDebugLogging)
            {
                _logger.LogDebug(
                    "Completed device health processing for device {DeviceId}",
                    deviceId
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing device health for device {DeviceId}", deviceId);
        }
    }

    /// <summary>
    /// Perform device-specific monitoring based on device type
    /// </summary>
    private async Task PerformDeviceSpecificMonitoringAsync(
        DeviceHealth device,
        ITrackerSuggestionService trackerSuggestionService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            switch (device.DeviceType)
            {
                case DeviceType.CGM:
                    await PerformCgmSpecificMonitoringAsync(device, trackerSuggestionService, cancellationToken);
                    break;
                case DeviceType.InsulinPump:
                    await PerformInsulinPumpSpecificMonitoringAsync(device, cancellationToken);
                    break;
                case DeviceType.BGM:
                    await PerformBgmSpecificMonitoringAsync(device, cancellationToken);
                    break;
                default:
                    // No device-specific monitoring for unknown devices
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during device-specific monitoring for device {DeviceId}",
                device.DeviceId
            );
        }
    }

    /// <summary>
    /// Minimum data gap in minutes to consider for sensor warmup suggestion
    /// </summary>
    private const int MinimumGapMinutesForWarmupSuggestion = 60;

    /// <summary>
    /// Perform CGM-specific monitoring
    /// </summary>
    private async Task PerformCgmSpecificMonitoringAsync(
        DeviceHealth device,
        ITrackerSuggestionService trackerSuggestionService,
        CancellationToken cancellationToken
    )
    {
        // Monitor sensor session duration
        if (device.SensorExpiration.HasValue)
        {
            var hoursUntilExpiration = (device.SensorExpiration.Value - DateTime.UtcNow).TotalHours;

            if (hoursUntilExpiration <= 0)
            {
                _logger.LogWarning("CGM sensor for device {DeviceId} has expired", device.DeviceId);
            }
            else if (hoursUntilExpiration <= 24)
            {
                _logger.LogInformation(
                    "CGM sensor for device {DeviceId} expires in {Hours:F1} hours",
                    device.DeviceId,
                    hoursUntilExpiration
                );
            }
        }

        // Monitor calibration status
        if (device.LastCalibration.HasValue)
        {
            var hoursSinceCalibration = (DateTime.UtcNow - device.LastCalibration.Value).TotalHours;

            if (hoursSinceCalibration > device.CalibrationReminderHours * 2)
            {
                _logger.LogWarning(
                    "CGM device {DeviceId} calibration is overdue by {Hours:F1} hours",
                    device.DeviceId,
                    hoursSinceCalibration - device.CalibrationReminderHours
                );
            }
        }

        // Check data continuity and evaluate for sensor tracker suggestion
        await CheckDataContinuityAsync(device, cancellationToken);

        // Check if there's a significant data gap that might indicate sensor warmup
        if (device.LastDataReceived.HasValue)
        {
            var minutesSinceLastData = (DateTime.UtcNow - device.LastDataReceived.Value).TotalMinutes;

            if (minutesSinceLastData >= MinimumGapMinutesForWarmupSuggestion)
            {
                // Evaluate for sensor tracker suggestion (might be a warmup)
                await EvaluateSensorWarmupSuggestionAsync(device, trackerSuggestionService, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Evaluate if a sensor tracker suggestion should be created based on data gap
    /// </summary>
    private async Task EvaluateSensorWarmupSuggestionAsync(
        DeviceHealth device,
        ITrackerSuggestionService trackerSuggestionService,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await trackerSuggestionService.EvaluateDataGapForTrackerSuggestionAsync(
                device.UserId,
                device.LastDataReceived!.Value,
                DateTime.UtcNow,
                cancellationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error evaluating sensor warmup suggestion for device {DeviceId}",
                device.DeviceId
            );
        }
    }

    /// <summary>
    /// Perform insulin pump-specific monitoring
    /// </summary>
    private async Task PerformInsulinPumpSpecificMonitoringAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    )
    {
        await CheckDataContinuityAsync(device, cancellationToken);

        _logger.LogDebug(
            "Performed insulin pump monitoring for device {DeviceId}",
            device.DeviceId
        );
    }

    /// <summary>
    /// Perform BGM-specific monitoring
    /// </summary>
    private async Task PerformBgmSpecificMonitoringAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    )
    {
        await CheckDataContinuityAsync(device, cancellationToken);

        // Check calibration status for BGM devices
        if (device.LastCalibration.HasValue)
        {
            var daysSinceCalibration = (DateTime.UtcNow - device.LastCalibration.Value).TotalDays;

            if (daysSinceCalibration > 30)
            {
                _logger.LogWarning(
                    "BGM device {DeviceId} has not been calibrated for {Days:F0} days",
                    device.DeviceId,
                    daysSinceCalibration
                );
            }
        }
    }

    /// <summary>
    /// Check data continuity for a device
    /// </summary>
    private Task CheckDataContinuityAsync(DeviceHealth device, CancellationToken cancellationToken)
    {
        if (!device.LastDataReceived.HasValue)
        {
            _logger.LogWarning("Device {DeviceId} has never received data", device.DeviceId);
            return Task.CompletedTask;
        }

        var minutesSinceLastData = (DateTime.UtcNow - device.LastDataReceived.Value).TotalMinutes;

        if (minutesSinceLastData > device.DataGapWarningMinutes)
        {
            var severity = minutesSinceLastData > 120 ? "critical" : "warning";
            _logger.LogWarning(
                "Device {DeviceId} has {Severity} data gap: {Minutes:F0} minutes since last data",
                device.DeviceId,
                severity,
                minutesSinceLastData
            );
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the background service
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Device Health Monitoring Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}

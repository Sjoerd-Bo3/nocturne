using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Services;

public interface IConnectorSyncService
{
    Task<SyncResult> TriggerSyncAsync(
        string connectorId,
        SyncRequest request,
        CancellationToken ct
    );
}

public class ConnectorSyncService : IConnectorSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ConnectorSyncService> _logger;
    private readonly ISyncProgressReporter _progressReporter;

    public ConnectorSyncService(
        IServiceProvider serviceProvider,
        ITenantAccessor tenantAccessor,
        ILogger<ConnectorSyncService> logger,
        ISyncProgressReporter progressReporter
    )
    {
        _serviceProvider = serviceProvider;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
        _progressReporter = progressReporter;
    }

    public async Task<SyncResult> TriggerSyncAsync(
        string connectorId,
        SyncRequest request,
        CancellationToken ct
    )
    {
        _logger.LogInformation("Manual sync triggered for connector {ConnectorId}", connectorId);

        try
        {
            using var scope = _serviceProvider.CreateScope();

            if (_tenantAccessor.Context is { } tenantContext)
            {
                var scopedTenantAccessor =
                    scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
                scopedTenantAccessor.SetTenant(tenantContext);
            }

            var executors = scope.ServiceProvider.GetServices<IConnectorSyncExecutor>();
            var executor = executors.FirstOrDefault(e =>
                e.ConnectorId.Equals(connectorId, StringComparison.OrdinalIgnoreCase));

            if (executor is null)
            {
                _logger.LogWarning(
                    "Unknown or disabled connector {ConnectorId}", connectorId);
                return new SyncResult
                {
                    Success = false,
                    Message = $"Unknown connector: {connectorId}",
                };
            }

            var result = await executor.ExecuteSyncAsync(scope.ServiceProvider, request, ct, _progressReporter);

            _logger.LogInformation(
                "Manual sync for {ConnectorId} completed: Success={Success}, Message={Message}",
                connectorId,
                result.Success,
                result.Message
            );

            return result;
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No service for type"))
        {
            _logger.LogWarning(
                "Connector {ConnectorId} is not registered (likely disabled)",
                connectorId
            );
            return new SyncResult
            {
                Success = false,
                Message = $"Connector '{connectorId}' is not configured or is disabled",
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error during manual sync for connector {ConnectorId}",
                connectorId
            );
            return new SyncResult { Success = false, Message = $"Sync failed: {ex.Message}" };
        }
    }
}

using Nocturne.API.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

public class ConnectorHealthService(
    IConfiguration configuration,
    IPostgreSqlService postgreSqlService,
    IConnectorConfigurationService connectorConfigService,
    ILogger<ConnectorHealthService> logger
) : IConnectorHealthService
{
    private record ConnectorDefinition(
        string Id, // Matches AvailableConnector.Id and health check name
        string ConfigKey,
        string DataSourceId
    ); // Maps to DataSources constant (e.g., "glooko-connector")

    private static IReadOnlyList<ConnectorDefinition> GetConnectorDefinitions()
    {
        return ConnectorMetadataService
            .GetAll()
            .Select(connector => new ConnectorDefinition(
                connector.ConnectorName.ToLowerInvariant(),
                connector.ConnectorName,
                connector.DataSourceId
            ))
            .ToList();
    }

    public async Task<IEnumerable<ConnectorStatusDto>> GetConnectorStatusesAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connectors = GetConnectorDefinitions();
        logger.LogDebug("Getting status for all {Count} connectors", connectors.Count);

        var results = new List<ConnectorStatusDto>();
        foreach (var connector in connectors)
        {
            var status = await GetConnectorStatusWithDbStatsAsync(connector, cancellationToken);
            results.Add(status);
        }

        // Filter out connectors that have no data and are not enabled (truly unused)
        return results
            .Where(r =>
                r.IsHealthy
                || // Running and healthy
                r.Status == "Disabled" && r.TotalEntries > 0
                || // Disabled but has historical data
                r.Status != "Disabled" // Any other status (unhealthy, unreachable, etc.)
            )
            .ToList();
    }

    private async Task<ConnectorStatusDto> GetConnectorStatusWithDbStatsAsync(
        ConnectorDefinition connector,
        CancellationToken cancellationToken
    )
    {
        // Query configuration once for both enabled state and health state
        var dbConfig = await connectorConfigService.GetConfigurationAsync(
            connector.Id,
            cancellationToken
        );

        // Determine enabled state (check environment config first)
        var envEnabled = configuration.GetValue<bool?>(
            $"Parameters:Connectors:{connector.ConfigKey}:Enabled"
        );
        var enabledConfig = envEnabled == false ? false : (dbConfig?.IsActive ?? envEnabled);

        // Extract health state from the same config
        ConnectorHealthStateDto? healthState = null;
        if (dbConfig != null)
        {
            healthState = new ConnectorHealthStateDto
            {
                LastSyncAttempt = dbConfig.LastSyncAttempt,
                LastSuccessfulSync = dbConfig.LastSuccessfulSync,
                LastErrorMessage = dbConfig.LastErrorMessage,
                LastErrorAt = dbConfig.LastErrorAt,
                IsHealthy = dbConfig.IsHealthy
            };
        }

        // Always get database stats for historical data (entries + treatments + state spans)
        var dbStats = await postgreSqlService.GetEntryStatsBySourceAsync(
            connector.DataSourceId,
            cancellationToken
        );

        logger.LogInformation(
            "Connector {Id}: EnabledConfig={EnabledConfig}, DataSourceId={DataSourceId}, TotalEntries={TotalEntries}, TotalTreatments={TotalTreatments}, TotalStateSpans={TotalStateSpans}",
            connector.Id,
            enabledConfig?.ToString() ?? "not configured",
            connector.DataSourceId,
            dbStats.TotalEntries,
            dbStats.TotalTreatments,
            dbStats.TotalStateSpans
        );

        // Use per-type breakdown dictionaries from database stats
        var totalBreakdown = dbStats.TypeBreakdown;
        var last24HBreakdown = dbStats.TypeBreakdownLast24Hours;

        // If explicitly disabled, return disabled status without checking health
        if (enabledConfig == false)
        {
            // Connector is explicitly disabled - return database-only stats
            // Use TotalItems which combines entries + treatments + state spans
            return new ConnectorStatusDto
            {
                Id = connector.Id,
                Name = connector.Id,
                Status = "Disabled",
                TotalEntries = dbStats.TotalItems,
                LastEntryTime = dbStats.LastItemTime,
                EntriesLast24Hours = dbStats.ItemsLast24Hours,
                State = "Disabled",
                IsHealthy = false,
                StateMessage = healthState?.LastErrorMessage,
                LastSyncAttempt = healthState?.LastSyncAttempt,
                LastSuccessfulSync = healthState?.LastSuccessfulSync,
                LastErrorAt = healthState?.LastErrorAt,
                TotalItemsBreakdown = totalBreakdown.Count > 0 ? totalBreakdown : null,
                ItemsLast24HoursBreakdown = last24HBreakdown.Count > 0 ? last24HBreakdown : null,
            };
        }

        var liveStatus = new ConnectorStatusDto
        {
            Id = connector.Id,
            Name = connector.Id,
            Status = enabledConfig == true ? "Running" : "Not Configured",
            IsHealthy = healthState?.IsHealthy ?? (enabledConfig == true),
            State = enabledConfig == true ? "Running" : "Not Configured",
            StateMessage = healthState?.LastErrorMessage,
            LastSyncAttempt = healthState?.LastSyncAttempt,
            LastSuccessfulSync = healthState?.LastSuccessfulSync,
            LastErrorAt = healthState?.LastErrorAt,
            // ALWAYS use database stats for entry counts - sidecar stats may be stale/cached
            // DB is the single source of truth for how much data exists
            TotalEntries = dbStats.TotalItems,
            LastEntryTime = dbStats.LastItemTime,
            EntriesLast24Hours = dbStats.ItemsLast24Hours,
            TotalItemsBreakdown = totalBreakdown.Count > 0 ? totalBreakdown : null,
            ItemsLast24HoursBreakdown = last24HBreakdown.Count > 0 ? last24HBreakdown : null,
        };

        return liveStatus;
    }
}

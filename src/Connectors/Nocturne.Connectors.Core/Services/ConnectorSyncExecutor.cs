using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Contracts;

namespace Nocturne.Connectors.Core.Services;

/// <summary>
///     Base class for connector sync executors. Handles service resolution,
///     DB config/secret loading, and delegation to the connector service.
/// </summary>
public abstract class ConnectorSyncExecutor<TService, TConfig> : IConnectorSyncExecutor
    where TService : class, IConnectorService<TConfig>
    where TConfig : class, IConnectorConfiguration
{
    public abstract string ConnectorId { get; }

    protected abstract string ConnectorName { get; }

    public async Task<SyncResult> ExecuteSyncAsync(
        IServiceProvider scopeProvider,
        SyncRequest request,
        CancellationToken ct,
        ISyncProgressReporter? progressReporter = null)
    {
        var service = scopeProvider.GetRequiredService<TService>();
        var config = scopeProvider.GetRequiredService<TConfig>();
        var logger = scopeProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(GetType());

        await LoadDatabaseConfigurationAsync(scopeProvider, config, logger, ct);

        return await service.SyncDataAsync(request, config, ct, progressReporter);
    }

    private async Task LoadDatabaseConfigurationAsync(
        IServiceProvider scopeProvider,
        TConfig config,
        ILogger logger,
        CancellationToken ct)
    {
        try
        {
            var configService = scopeProvider.GetRequiredService<IConnectorConfigurationService>();

            var dbConfig = await configService.GetConfigurationAsync(ConnectorName, ct);
            if (dbConfig?.Configuration != null)
                ApplyJsonToConfig(dbConfig.Configuration, config);

            var secrets = await configService.GetSecretsAsync(ConnectorName, ct);
            if (secrets.Count > 0)
                ApplySecretsToConfig(secrets, config);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load database configuration for {ConnectorName} during manual sync",
                ConnectorName);
        }
    }

    private static void ApplyJsonToConfig(JsonDocument configuration, TConfig config)
    {
        var properties = config.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var root = configuration.RootElement;

        foreach (var property in properties)
        {
            if (!property.CanWrite)
                continue;

            var camelName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            if (!root.TryGetProperty(camelName, out var element))
                continue;

            try
            {
                if (property.PropertyType == typeof(string)
                    && element.ValueKind == JsonValueKind.String)
                    property.SetValue(config, element.GetString());
                else if (property.PropertyType == typeof(int)
                    && element.ValueKind == JsonValueKind.Number)
                    property.SetValue(config, element.GetInt32());
                else if (property.PropertyType == typeof(double)
                    && element.ValueKind == JsonValueKind.Number)
                    property.SetValue(config, element.GetDouble());
                else if (property.PropertyType == typeof(bool)
                    && (element.ValueKind == JsonValueKind.True
                        || element.ValueKind == JsonValueKind.False))
                    property.SetValue(config, element.GetBoolean());
            }
            catch (Exception)
            {
                // Skip properties that can't be set
            }
        }
    }

    private static void ApplySecretsToConfig(
        Dictionary<string, string> secrets, TConfig config)
    {
        var properties = config.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanWrite || property.PropertyType != typeof(string))
                continue;

            var camelName = char.ToLowerInvariant(property.Name[0]) + property.Name[1..];
            if (secrets.TryGetValue(camelName, out var value))
                property.SetValue(config, value);
        }
    }
}

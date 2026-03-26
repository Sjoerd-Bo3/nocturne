using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Tidepool.Configurations;
using Nocturne.Connectors.Tidepool.Services;

namespace Nocturne.Connectors.Tidepool;

public class TidepoolConnectorInstaller : IConnectorInstaller
{
    public string ConnectorName => "Tidepool";

    public void Install(IServiceCollection services, IConfiguration configuration)
    {
        var config = services.AddConnector<TidepoolConnectorConfiguration, TidepoolConnectorService, TidepoolAuthTokenProvider>(
            configuration,
            new TidepoolConnectorOptions());

        if (config == null)
            return;

        services.AddConnectorTokenProvider<TidepoolAuthTokenProvider>();
        services.AddConnectorSyncExecutor<TidepoolSyncExecutor>();
    }

    private sealed class TidepoolConnectorOptions : ConnectorOptions
    {
        [SetsRequiredMembers]
        public TidepoolConnectorOptions()
        {
            ConnectorName = "Tidepool";
            ServerMapping = new Dictionary<string, string>
            {
                ["US"] = TidepoolConstants.Servers.Us,
                ["Development"] = TidepoolConstants.Servers.Development
            };
            GetServerRegion = config => ((TidepoolConnectorConfiguration)config).Server;
        }
    }
}

public class TidepoolSyncExecutor
    : ConnectorSyncExecutor<TidepoolConnectorService, TidepoolConnectorConfiguration>
{
    public override string ConnectorId => "tidepool";

    protected override string ConnectorName => "Tidepool";
}

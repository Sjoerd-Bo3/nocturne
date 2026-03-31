using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Nightscout.Configurations;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Nightscout.Services.WriteBack;

/// <summary>
/// Writes treatment data back to the upstream Nightscout instance.
/// Skips treatments that originated from the Nightscout connector to prevent sync loops.
/// </summary>
public class NightscoutTreatmentWriteBackSink(
    HttpClient httpClient,
    NightscoutConnectorConfiguration config,
    NightscoutCircuitBreaker circuitBreaker,
    ILogger<NightscoutTreatmentWriteBackSink> logger)
    : NightscoutWriteBackSink<Treatment>(httpClient, config, circuitBreaker, logger)
{
    protected override string Endpoint => "/api/v1/treatments";

    protected override bool ShouldSkip(Treatment item)
        => item.DataSource == DataSources.NightscoutConnector;
}

using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Nightscout.Configurations;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Nightscout.Services.WriteBack;

/// <summary>
/// Writes activity data back to the upstream Nightscout instance.
/// </summary>
public class NightscoutActivityWriteBackSink(
    HttpClient httpClient,
    NightscoutConnectorConfiguration config,
    NightscoutCircuitBreaker circuitBreaker,
    ILogger<NightscoutActivityWriteBackSink> logger)
    : NightscoutWriteBackSink<Activity>(httpClient, config, circuitBreaker, logger)
{
    protected override string Endpoint => "/api/v1/activity";
}

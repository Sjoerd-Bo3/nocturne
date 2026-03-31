using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Nightscout.Configurations;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Nightscout.Services.WriteBack;

/// <summary>
/// Writes profile data back to the upstream Nightscout instance.
/// </summary>
public class NightscoutProfileWriteBackSink(
    HttpClient httpClient,
    NightscoutConnectorConfiguration config,
    NightscoutCircuitBreaker circuitBreaker,
    ILogger<NightscoutProfileWriteBackSink> logger)
    : NightscoutWriteBackSink<Profile>(httpClient, config, circuitBreaker, logger)
{
    protected override string Endpoint => "/api/v1/profile";
}

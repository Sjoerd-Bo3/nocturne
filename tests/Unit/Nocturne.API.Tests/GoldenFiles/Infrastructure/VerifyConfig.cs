using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace Nocturne.API.Tests.GoldenFiles.Infrastructure;

/// <summary>
/// Global Verify configuration. Module initializer runs automatically before any tests.
/// </summary>
public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        // Scrub dynamic server timestamps that change between runs.
        // These appear as JSON properties in Nightscout API responses.
        VerifierSettings.AddScrubber(builder =>
        {
            var text = builder.ToString();
            // Scrub serverTime, serverTimeEpoch, and srvDate values
            text = Regex.Replace(text, @"""serverTime""\s*:\s*""[^""]*""", @"""serverTime"": ""{scrubbed}""");
            text = Regex.Replace(text, @"""serverTimeEpoch""\s*:\s*\d+", @"""serverTimeEpoch"": ""{scrubbed}""");
            text = Regex.Replace(text, @"""srvDate""\s*:\s*\d+", @"""srvDate"": ""{scrubbed}""");
            // Scrub traceId from problem+json error responses
            text = Regex.Replace(text, @"""traceId""\s*:\s*""[^""]*""", @"""traceId"": ""{scrubbed}""");
            builder.Clear();
            builder.Append(text);
        });
    }
}

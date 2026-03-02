using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.MyLife.Configurations.Constants;

namespace Nocturne.Connectors.MyLife.Services;

public class MyLifeRestClient(HttpClient httpClient, ILogger<MyLifeRestClient> logger)
{
    /// <summary>
    /// Authenticates with the MyLife REST API.
    /// Auth request format is not yet known - this is scaffolding for future discovery.
    /// </summary>
    public async Task<string?> AuthenticateAsync(
        string restServiceUrl,
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        var url = CombineUrl(restServiceUrl, MyLifeConstants.RestPaths.Auth);

        var payload = JsonSerializer.Serialize(new { login, password });
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        var response = await httpClient.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        logger.LogDebug(
            "MyLife REST auth response: {StatusCode} {Body}",
            (int)response.StatusCode,
            content);

        if (response.IsSuccessStatusCode) return content;

        return null;
    }

    private static string CombineUrl(string serviceUrl, string path)
    {
        var baseUrl = serviceUrl.TrimEnd('/');
        var suffix = path.TrimStart('/');
        return $"{baseUrl}/{suffix}";
    }
}

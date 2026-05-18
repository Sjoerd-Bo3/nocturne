using Nocturne.API.Models.Compatibility;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Clones an incoming ASP.NET Core <see cref="HttpRequest"/> into a serialisable
/// <see cref="Models.Compatibility.ClonedRequest"/> for forwarding to the upstream Nightscout instance.
/// </summary>
/// <seealso cref="RequestCloningService"/>
public interface IRequestCloningService
{
    /// <summary>
    /// Clones the request, capturing method, path+query, headers (minus hop-by-hop headers),
    /// and body bytes. The original request body stream is reset if seekable so the pipeline
    /// can still read it.
    /// </summary>
    Task<ClonedRequest> CloneRequestAsync(HttpRequest request);
}

/// <summary>
/// <see cref="IRequestCloningService"/> implementation that buffers the request body into a
/// <see cref="MemoryStream"/> and filters hop-by-hop headers that must not be forwarded
/// (<c>host</c>, <c>connection</c>, <c>content-length</c>, <c>transfer-encoding</c>,
/// <c>upgrade</c>, <c>proxy-*</c>).
/// </summary>
/// <remarks>
/// For non-seekable request bodies (common in integration tests), the body is replaced with
/// a new <see cref="MemoryStream"/> backed by the captured bytes so subsequent middleware or
/// model binding can still read it.
/// </remarks>
/// <seealso cref="IRequestCloningService"/>
public class RequestCloningService : IRequestCloningService
{
    private readonly ILogger<RequestCloningService> _logger;

    /// <summary>
    /// Initializes a new instance of the RequestCloningService class
    /// </summary>
    /// <param name="logger">Logger instance for this service</param>
    public RequestCloningService(ILogger<RequestCloningService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClonedRequest> CloneRequestAsync(HttpRequest request)
    {
        _logger.LogDebug("Cloning request: {Method} {Path}", request.Method, request.Path);

        var clonedRequest = new ClonedRequest
        {
            Method = request.Method,
            Path = request.Path + request.QueryString,
            ContentType = request.ContentType,
        };

        // Clone headers
        foreach (var header in request.Headers)
        {
            // Skip headers that shouldn't be forwarded
            if (ShouldForwardHeader(header.Key))
            {
                clonedRequest.Headers[header.Key] = header.Value.ToArray()!;
            }
        }

        // Clone query parameters
        foreach (var query in request.Query)
        {
            clonedRequest.QueryParameters[query.Key] = query.Value.ToArray()!;
        }

        // Clone body if present
        if (request.ContentLength > 0 && request.Body.CanRead)
        {
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            clonedRequest.Body = memoryStream.ToArray();

            // Reset the original request body stream for potential reuse if possible
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
            else
            {
                // For non-seekable streams (like in testing), we need to replace the stream
                request.Body = new MemoryStream(clonedRequest.Body);
            }
        }

        _logger.LogDebug(
            "Request cloned successfully. Headers: {HeaderCount}, Body size: {BodySize}",
            clonedRequest.Headers.Count,
            clonedRequest.Body?.Length ?? 0
        );

        return clonedRequest;
    }

    private static bool ShouldForwardHeader(string headerName)
    {
        // Skip host-specific and connection-specific headers
        var skipHeaders = new[]
        {
            "host",
            "connection",
            "content-length",
            "transfer-encoding",
            "upgrade",
            "proxy-connection",
            "proxy-authenticate",
            "proxy-authorization",
        };

        return !skipHeaders.Contains(headerName.ToLowerInvariant());
    }
}

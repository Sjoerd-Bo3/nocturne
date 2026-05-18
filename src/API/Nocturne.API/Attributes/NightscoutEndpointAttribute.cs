namespace Nocturne.API.Attributes;

/// <summary>
/// Attribute to mark controller methods with their corresponding Nightscout endpoint.
/// This provides documentation and traceability for 1:1 API compatibility.
/// </summary>
/// <remarks>
/// Applied to v1/v2/v3 controller actions to record the original Nightscout endpoint path.
/// Used alongside <see cref="Middleware.JsonExtensionMiddleware"/> which strips <c>.json</c>
/// extensions from incoming requests to match these routes. The
/// <see cref="Middleware.CompatibilityProxyMiddleware"/> can then compare Nocturne's response
/// against the upstream Nightscout instance for marked endpoints.
/// </remarks>
/// <seealso cref="Middleware.JsonExtensionMiddleware"/>
/// <seealso cref="Middleware.CompatibilityProxyMiddleware"/>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class NightscoutEndpointAttribute : Attribute
{
    /// <summary>
    /// The Nightscout endpoint this method implements
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="NightscoutEndpointAttribute"/>.
    /// </summary>
    /// <param name="endpoint">The Nightscout endpoint this method implements (e.g., <c>/api/v1/profile</c>).</param>
    public NightscoutEndpointAttribute(string endpoint)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }
}

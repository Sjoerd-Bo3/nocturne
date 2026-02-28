using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.API.Models.Compatibility;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Service for caching responses to avoid duplicate requests
/// </summary>
public interface IResponseCacheService
{
    /// <summary>
    /// Generate a cache key for a request
    /// </summary>
    /// <param name="clonedRequest">The request to generate a key for</param>
    /// <returns>Cache key string</returns>
    string GenerateCacheKey(ClonedRequest clonedRequest);

    /// <summary>
    /// Try to get a cached response
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <returns>Cached response if found, null otherwise</returns>
    Task<CompatibilityProxyResponse?> GetCachedResponseAsync(string cacheKey);

    /// <summary>
    /// Cache a response
    /// </summary>
    /// <param name="cacheKey">Cache key</param>
    /// <param name="response">Response to cache</param>
    /// <returns>Task representing the async operation</returns>
    Task SetCachedResponseAsync(string cacheKey, CompatibilityProxyResponse response);

    /// <summary>
    /// Check if a request should be cached
    /// </summary>
    /// <param name="clonedRequest">Request to check</param>
    /// <returns>True if the request should be cached</returns>
    bool ShouldCacheRequest(ClonedRequest clonedRequest);
}

/// <summary>
/// Implementation of response cache service using in-memory caching
/// </summary>
public class ResponseCacheService : IResponseCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IOptions<CompatibilityProxyConfiguration> _configuration;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ResponseCacheService> _logger;

    // HTTP methods that are safe to cache
    private static readonly HashSet<string> CacheableMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET",
        "HEAD",
        "OPTIONS",
    };

    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    /// <summary>
    /// Initializes a new instance of the ResponseCacheService class
    /// </summary>
    /// <param name="memoryCache">Memory cache for storing responses</param>
    /// <param name="configuration">Compatibility proxy configuration settings</param>
    /// <param name="tenantAccessor">Tenant accessor for scoping cache keys</param>
    /// <param name="logger">Logger instance for this service</param>
    public ResponseCacheService(
        IMemoryCache memoryCache,
        IOptions<CompatibilityProxyConfiguration> configuration,
        ITenantAccessor tenantAccessor,
        ILogger<ResponseCacheService> logger
    )
    {
        _memoryCache = memoryCache;
        _configuration = configuration;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string GenerateCacheKey(ClonedRequest clonedRequest)
    {
        if (!_configuration.Value.EnableResponseCaching)
        {
            return string.Empty;
        }

        try
        {
            // Create a hash based on method, path, and relevant headers
            var keyBuilder = new StringBuilder();
            keyBuilder.Append(clonedRequest.Method);
            keyBuilder.Append('|');
            keyBuilder.Append(clonedRequest.Path);
            keyBuilder.Append('|');

            // Include authorization headers in cache key to prevent cross-user cache hits
            if (clonedRequest.Headers.TryGetValue("Authorization", out var authValues))
            {
                keyBuilder.Append(string.Join(",", authValues));
            }
            if (clonedRequest.Headers.TryGetValue("api-secret", out var apiSecretValues))
            {
                keyBuilder.Append(string.Join(",", apiSecretValues));
            }

            // Include request body for POST-like requests (though we typically don't cache these)
            if (clonedRequest.Body?.Length > 0)
            {
                using var sha256 = SHA256.Create();
                var bodyHash = Convert.ToBase64String(sha256.ComputeHash(clonedRequest.Body));
                keyBuilder.Append('|');
                keyBuilder.Append(bodyHash);
            }

            var fullKey = keyBuilder.ToString();

            // Generate a shorter hash for the cache key
            using var sha = SHA256.Create();
            var keyHash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(fullKey)));

            var cacheKey = $"compatibility_proxy_cache:{TenantCacheId}:{keyHash}";

            _logger.LogDebug(
                "Generated cache key for {Method} {Path}: {CacheKey}",
                clonedRequest.Method,
                clonedRequest.Path,
                cacheKey
            );

            return cacheKey;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error generating cache key for {Method} {Path}",
                clonedRequest.Method,
                clonedRequest.Path
            );
            return string.Empty;
        }
    }

    /// <inheritdoc />
    public Task<CompatibilityProxyResponse?> GetCachedResponseAsync(string cacheKey)
    {
        if (!_configuration.Value.EnableResponseCaching || string.IsNullOrEmpty(cacheKey))
        {
            return Task.FromResult<CompatibilityProxyResponse?>(null);
        }

        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out CompatibilityProxyResponse? cachedResponse))
            {
                _logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                return Task.FromResult<CompatibilityProxyResponse?>(cachedResponse);
            }

            _logger.LogDebug("Cache miss for key: {CacheKey}", cacheKey);
            return Task.FromResult<CompatibilityProxyResponse?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error retrieving cached response for key: {CacheKey}",
                cacheKey
            );
            return Task.FromResult<CompatibilityProxyResponse?>(null);
        }
    }

    /// <inheritdoc />
    public Task SetCachedResponseAsync(string cacheKey, CompatibilityProxyResponse response)
    {
        if (!_configuration.Value.EnableResponseCaching || string.IsNullOrEmpty(cacheKey))
        {
            return Task.CompletedTask;
        }

        try
        {
            var ttl = TimeSpan.FromSeconds(_configuration.Value.ResponseCacheTtlSeconds);
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = ttl,
                SlidingExpiration = TimeSpan.FromSeconds(ttl.TotalSeconds / 4), // Sliding window is 1/4 of absolute
                Priority = CacheItemPriority.Normal,
            };

            // Don't cache responses with errors
            if (response.SelectedResponse?.IsSuccess != true)
            {
                _logger.LogDebug("Not caching error response for key: {CacheKey}", cacheKey);
                return Task.CompletedTask;
            }

            // Don't cache very large responses
            var maxResponseSize = 1024 * 1024; // 1MB
            if (response.SelectedResponse?.Body?.Length > maxResponseSize)
            {
                _logger.LogDebug(
                    "Not caching large response ({Size} bytes) for key: {CacheKey}",
                    response.SelectedResponse.Body.Length,
                    cacheKey
                );
                return Task.CompletedTask;
            }

            _memoryCache.Set(cacheKey, response, cacheOptions);

            _logger.LogDebug(
                "Cached response for key: {CacheKey}, TTL: {TTL}s",
                cacheKey,
                ttl.TotalSeconds
            );
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error caching response for key: {CacheKey}", cacheKey);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public bool ShouldCacheRequest(ClonedRequest clonedRequest)
    {
        if (!_configuration.Value.EnableResponseCaching)
        {
            return false;
        }

        // Only cache safe HTTP methods
        if (!CacheableMethods.Contains(clonedRequest.Method))
        {
            return false;
        }

        // Don't cache requests with bodies (typically unsafe operations)
        if (clonedRequest.Body?.Length > 0)
        {
            return false;
        }

        // Don't cache requests to certain endpoints that are expected to change frequently
        var path = clonedRequest.Path.ToLowerInvariant();
        var uncacheablePaths = new[] { "/status", "/heartbeat", "/health", "/time" };

        if (uncacheablePaths.Any(p => path.Contains(p)))
        {
            return false;
        }

        return true;
    }
}

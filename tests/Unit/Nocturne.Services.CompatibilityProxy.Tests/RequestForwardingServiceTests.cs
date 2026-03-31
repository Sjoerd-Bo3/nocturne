using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Configuration;
using Nocturne.API.Models.Compatibility;
using Nocturne.API.Services.Compatibility;
using Nocturne.Connectors.Nightscout.Configurations;
using Nocturne.Connectors.Nightscout.Services.WriteBack;
using Xunit;

namespace Nocturne.Services.CompatibilityProxy.Tests.Unit;

public class RequestForwardingServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ICorrelationService> _correlationServiceMock;
    private readonly NightscoutCircuitBreaker _circuitBreaker;
    private readonly ILogger<RequestForwardingService> _logger;

    public RequestForwardingServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _correlationServiceMock = new Mock<ICorrelationService>();
        _circuitBreaker = new NightscoutCircuitBreaker();
        _logger = new LoggerFactory().CreateLogger<RequestForwardingService>();
    }

    private RequestForwardingService CreateService(
        CompatibilityProxyConfiguration? proxyConfig = null,
        NightscoutConnectorConfiguration? nightscoutConfig = null)
    {
        var options = Options.Create(proxyConfig ?? new CompatibilityProxyConfiguration());
        return new RequestForwardingService(
            _httpClientFactoryMock.Object,
            options,
            nightscoutConfig ?? new NightscoutConnectorConfiguration(),
            _circuitBreaker,
            _correlationServiceMock.Object,
            _logger
        );
    }

    [Fact]
    public async Task ForwardToNightscoutAsync_CircuitBreakerOpen_ShouldReturnNull()
    {
        // Arrange — trip the circuit breaker
        for (int i = 0; i < 10; i++)
            _circuitBreaker.RecordFailure();

        var service = CreateService();
        var request = new ClonedRequest { Method = "GET", Path = "/api/v1/entries" };

        // Act
        var result = await service.ForwardToNightscoutAsync(request);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task ForwardToNightscoutAsync_NoUrl_ShouldReturnErrorResponse()
    {
        // Arrange — connector config with empty URL
        var nightscoutConfig = new NightscoutConnectorConfiguration { Url = "" };
        var service = CreateService(nightscoutConfig: nightscoutConfig);
        var request = new ClonedRequest { Method = "GET", Path = "/api/v1/entries" };

        // Act
        var result = await service.ForwardToNightscoutAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Nightscout URL not configured", result.ErrorMessage);
    }

    [Fact]
    public void FilterSensitiveErrorMessage_ContainsSensitiveData_ShouldRedactFieldNames()
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            Redaction = new RedactionSettings
            {
                SensitiveFields = new List<string> { "custom_field" },
            },
        };

        var service = CreateService(proxyConfig: config);
        var errorMessage = "Authentication failed with api_secret=12345 and token=abcdef";

        // Use reflection to access the private FilterSensitiveErrorMessage method
        var filterMethod = typeof(RequestForwardingService).GetMethod(
            "FilterSensitiveErrorMessage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var filtered = (string)filterMethod!.Invoke(service, new object[] { errorMessage })!;

        // Assert
        Assert.Contains("[REDACTED]", filtered);
        Assert.DoesNotContain("api_secret", filtered);
        Assert.DoesNotContain("token", filtered);
    }
}

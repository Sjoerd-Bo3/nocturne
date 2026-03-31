using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Infrastructure.Data;
using Nocturne.API.Configuration;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Nocturne.Services.CompatibilityProxy.Integration.Tests;

public class CompatibilityProxyServiceIntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>,
        IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;
    private readonly WireMockServer _nightscoutMockServer;
    private readonly WireMockServer _nocturneeMockServer;

    public CompatibilityProxyServiceIntegrationTests(WebApplicationFactory<Program> factory)
    {
        // Start mock servers
        _nightscoutMockServer = WireMockServer.Start();
        _nocturneeMockServer = WireMockServer.Start();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace configuration with test URLs
                services.Configure<CompatibilityProxyConfiguration>(config =>
                {
                    config.Enabled = true;
                    config.TimeoutSeconds = 5;
                    config.RetryAttempts = 1;
                });

                // Remove existing DbContext registration and use in-memory database
                var dbContextDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<NocturneDbContext>)
                );
                if (dbContextDescriptor != null)
                {
                    services.Remove(dbContextDescriptor);
                }

                var dbContextServiceDescriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(NocturneDbContext)
                );
                if (dbContextServiceDescriptor != null)
                {
                    services.Remove(dbContextServiceDescriptor);
                }

                // Add in-memory database for testing
                services.AddDbContext<NocturneDbContext>(options =>
                {
                    options.UseInMemoryDatabase("CompatibilityProxyTestDb");
                });
            });
        });

        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task GetRequest_ShouldForwardToTargetSystems_AndReturnNightscoutResponse()
    {
        // Arrange
        var nightscoutResponse = """{"status": "ok", "from": "nightscout"}""";
        var nocturneResponse = """{"status": "ok", "from": "nocturne"}""";

        _nightscoutMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(nightscoutResponse)
            );

        _nocturneeMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(nocturneResponse)
            );

        // Act
        var response = await _client.GetAsync("/api/v1/status");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(nightscoutResponse, content); // Should return Nightscout response by default
        Assert.Contains("application/json", response.Content.Headers.ContentType?.ToString());

        // Verify both servers were called
        Assert.Single(_nightscoutMockServer.LogEntries);
        Assert.Single(_nocturneeMockServer.LogEntries);
    }

    [Fact]
    public async Task PostRequest_ShouldForwardBodyContent_ToTargetSystems()
    {
        // Arrange
        var requestBody = """{"type": "sgv", "sgv": 120, "direction": "Flat"}""";
        var nightscoutResponse = """{"ok": true, "id": "nightscout-123"}""";
        var nocturneResponse = """{"ok": true, "id": "nocturne-456"}""";

        _nightscoutMockServer
            .Given(Request.Create().WithPath("/api/v1/entries").UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(nightscoutResponse)
            );

        _nocturneeMockServer
            .Given(Request.Create().WithPath("/api/v1/entries").UsingPost())
            .RespondWith(
                Response
                    .Create()
                    .WithStatusCode(201)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(nocturneResponse)
            );

        // Act
        var response = await _client.PostAsync(
            "/api/v1/entries",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
        );
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(nightscoutResponse, content); // Should return Nightscout response by default

        // Verify both servers received the POST with body
        var nightscoutLog = _nightscoutMockServer.LogEntries.First();
        var nocturneLog = _nocturneeMockServer.LogEntries.First();

        Assert.Equal("POST", nightscoutLog.RequestMessage.Method);
        Assert.Equal("POST", nocturneLog.RequestMessage.Method);
        Assert.Contains("sgv", nightscoutLog.RequestMessage.Body ?? "");
        Assert.Contains("sgv", nocturneLog.RequestMessage.Body ?? "");
    }

    [Fact]
    public async Task Request_WhenNightscoutFails_ShouldReturnNightscoutTimeoutWithDefaultStrategy()
    {
        // Arrange
        _nightscoutMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("Nightscout error"));

        _nocturneeMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Nocturne success"));

        // Act
        var response = await _client.GetAsync("/api/v1/status");

        // Assert
        // Due to resilience retry policy, the 500 response causes retries that eventually timeout
        // The default strategy is to return Nightscout response even when it fails/times out
        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
    }

    [Fact]
    public async Task Request_WhenNightscoutReturnsErrorDirectly_ShouldReturnNightscoutError()
    {
        // Arrange - Use a 400 error that won't trigger retries
        _nightscoutMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(400).WithBody("Bad request"));

        _nocturneeMockServer
            .Given(Request.Create().WithPath("/api/v1/status").UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody("Nocturne success"));

        // Act
        var response = await _client.GetAsync("/api/v1/status");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Bad request", content); // Should return Nightscout response
    }

    [Fact]
    public async Task ServiceInfo_ShouldReturnCorrectInformation()
    {
        // Act
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Nocturne Compatibility Proxy Service", content);
        Assert.Contains("version", content);
        Assert.Contains("health", content);
        Assert.Contains("swagger", content);
    }

    public async ValueTask DisposeAsync()
    {
        _nightscoutMockServer?.Stop();
        _nightscoutMockServer?.Dispose();
        _nocturneeMockServer?.Stop();
        _nocturneeMockServer?.Dispose();
        _client?.Dispose();
        await _factory.DisposeAsync();
    }
}

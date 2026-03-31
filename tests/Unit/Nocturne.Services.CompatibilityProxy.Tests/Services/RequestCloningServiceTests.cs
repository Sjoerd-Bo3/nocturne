using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Core.Models;
using Nocturne.API.Services.Compatibility;
using Xunit;

namespace Nocturne.Services.CompatibilityProxy.Tests.Services;

public class RequestCloningServiceTests
{
    private readonly Mock<ILogger<RequestCloningService>> _mockLogger = new();
    private readonly RequestCloningService _service;

    public RequestCloningServiceTests()
    {
        _service = new RequestCloningService(_mockLogger.Object);
    }

    [Fact]
    public async Task CloneRequestAsync_ShouldCloneBasicGetRequest()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/entries";
        context.Request.QueryString = new QueryString("?count=10");

        // Act
        var result = await _service.CloneRequestAsync(context.Request);

        // Assert
        Assert.Equal("GET", result.Method);
        Assert.Equal("/api/entries?count=10", result.Path);
        Assert.Empty(result.Headers);
        Assert.Null(result.Body);
    }

    [Fact]
    public async Task CloneRequestAsync_ShouldCloneHeaders()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/entries";
        context.Request.Headers.Append("Authorization", "Bearer token123");
        context.Request.Headers.Append("Custom-Header", "custom-value");
        context.Request.Headers.Append("Host", "localhost:5000"); // Should be filtered out

        // Act
        var result = await _service.CloneRequestAsync(context.Request);

        // Assert
        Assert.Contains("Authorization", result.Headers);
        Assert.Contains("Custom-Header", result.Headers);
        Assert.DoesNotContain("Host", result.Headers);
        Assert.Equal("Bearer token123", result.Headers["Authorization"][0]);
        Assert.Equal("custom-value", result.Headers["Custom-Header"][0]);
    }

    [Fact]
    public async Task CloneRequestAsync_ShouldCloneQueryParameters()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/entries";
        context.Request.Query = new QueryCollection(
            new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                ["count"] = "10",
                ["find[dateString]"] = "2023-10-01",
            }
        );

        // Act
        var result = await _service.CloneRequestAsync(context.Request);

        // Assert
        Assert.Contains("count", result.QueryParameters);
        Assert.Contains("find[dateString]", result.QueryParameters);
        Assert.Equal("10", result.QueryParameters["count"][0]);
        Assert.Equal("2023-10-01", result.QueryParameters["find[dateString]"][0]);
    }

    [Fact]
    public async Task CloneRequestAsync_ShouldCloneRequestBody()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/treatments";
        context.Request.ContentType = "application/json";

        var bodyContent = "{\"eventType\":\"BG Check\",\"glucose\":120}";
        var bodyBytes = Encoding.UTF8.GetBytes(bodyContent);
        context.Request.Body = new MemoryStream(bodyBytes);
        context.Request.ContentLength = bodyBytes.Length;

        // Act
        var result = await _service.CloneRequestAsync(context.Request);

        // Assert
        Assert.Equal("POST", result.Method);
        Assert.Equal("application/json", result.ContentType);
        Assert.NotNull(result.Body);
        Assert.Equal(bodyBytes, result.Body);
    }

    [Fact]
    public async Task CloneRequestAsync_ShouldHandleEmptyBody()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/status";
        context.Request.Body = new MemoryStream();
        context.Request.ContentLength = 0;

        // Act
        var result = await _service.CloneRequestAsync(context.Request);

        // Assert
        Assert.Equal("GET", result.Method);
        Assert.Null(result.Body);
    }
}

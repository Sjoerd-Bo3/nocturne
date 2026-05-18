using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V1;
using Nocturne.Core.Contracts.Platform;
using Nocturne.Core.Contracts.Identity;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V1;

/// <summary>
/// Unit tests for AlexaController
/// Tests maintain 1:1 compatibility with legacy Alexa API endpoint
/// </summary>
public class AlexaControllerTests
{
    private readonly Mock<IAlexaService> _mockAlexaService;
    private readonly Mock<IAuthorizationService> _mockAuthorizationService;
    private readonly Mock<ILogger<AlexaController>> _mockLogger;
    private readonly AlexaController _controller;

    public AlexaControllerTests()
    {
        _mockAlexaService = new Mock<IAlexaService>();
        _mockAuthorizationService = new Mock<IAuthorizationService>();
        _mockLogger = new Mock<ILogger<AlexaController>>();
        _controller = new AlexaController(
            _mockAlexaService.Object,
            _mockAuthorizationService.Object,
            _mockLogger.Object
        );

        // Set up HttpContext for the controller
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                Connection = { RemoteIpAddress = IPAddress.Parse("127.0.0.1") },
            },
        };
    }

    [Fact]
    public async Task HandleAlexaRequest_ValidRequest_Authorized_ReturnsOkResponse()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest", Locale = "en-US" },
        };

        var expectedResponse = new AlexaResponse
        {
            Version = "1.0",
            Response = new AlexaResponseDetails
            {
                OutputSpeech = new AlexaOutputSpeech
                {
                    Type = "PlainText",
                    Text = "Hello, I can help you check your blood sugar.",
                },
                ShouldEndSession = false,
            },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlexaResponse>(okResult.Value);
        Assert.Equal(expectedResponse.Version, response.Version);
        Assert.Equal(
            expectedResponse.Response.OutputSpeech?.Text,
            response.Response.OutputSpeech?.Text
        );
    }

    [Fact]
    public async Task HandleAlexaRequest_ValidRequest_Unauthorized_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest", Locale = "en-US" },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Access denied", unauthorizedResult.Value);
    }

    [Fact]
    public async Task HandleAlexaRequest_NullRequest_ReturnsBadRequest()
    {
        // Arrange
        AlexaRequest? request = null;

        // Act
        var result = await _controller.HandleAlexaRequest(request!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid Alexa request format", badRequestResult.Value);
    }

    [Fact]
    public async Task HandleAlexaRequest_NullRequestDetails_ReturnsBadRequest()
    {
        // Arrange
        var request = new AlexaRequest { Request = null! };

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid Alexa request format", badRequestResult.Value);
    }

    [Fact]
    public async Task HandleAlexaRequest_ServiceThrowsArgumentException_ReturnsBadRequest()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest" },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Invalid request format"));

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal("Invalid request format", badRequestResult.Value);
    }

    [Fact]
    public async Task HandleAlexaRequest_ServiceThrowsUnauthorizedException_ReturnsUnauthorized()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest" },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Access denied"));

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result.Result);
        Assert.Equal("Access denied", unauthorizedResult.Value);
    }

    [Fact]
    public async Task HandleAlexaRequest_ServiceThrowsGenericException_ReturnsAlexaErrorResponse()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest" },
        };

        var errorResponse = new AlexaResponse
        {
            Version = "1.0",
            Response = new AlexaResponseDetails
            {
                OutputSpeech = new AlexaOutputSpeech
                {
                    Type = "PlainText",
                    Text = "Sorry, I'm having trouble right now. Please try again later.",
                },
                ShouldEndSession = true,
            },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database connection failed"));

        _mockAlexaService
            .Setup(x =>
                x.BuildSpeechletResponse(
                    "Error",
                    "Sorry, I'm having trouble right now. Please try again later.",
                    string.Empty,
                    true
                )
            )
            .Returns(errorResponse);

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AlexaResponse>(okResult.Value);
        Assert.Equal(
            errorResponse.Response.OutputSpeech?.Text,
            response.Response.OutputSpeech?.Text
        );
        Assert.True(response.Response.ShouldEndSession);
    }

    [Fact]
    public async Task HandleAlexaRequest_IntentRequest_LogsRequestType()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails
            {
                Type = "IntentRequest",
                Intent = new AlexaIntent { Name = "NSStatus" },
            },
        };

        var response = new AlexaResponse
        {
            Version = "1.0",
            Response = new AlexaResponseDetails { ShouldEndSession = true },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AlexaResponse>(okResult.Value);

        // Verify logging was called appropriately
        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("Incoming request from Alexa")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );

        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) =>
                            v.ToString()!
                                .Contains("Successfully processed Alexa IntentRequest request")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task HandleAlexaRequest_WithLocale_LogsLocaleInformation()
    {
        // Arrange
        var request = new AlexaRequest
        {
            Request = new AlexaRequestDetails { Type = "LaunchRequest", Locale = "es-ES" },
        };

        var response = new AlexaResponse
        {
            Version = "1.0",
            Response = new AlexaResponseDetails { ShouldEndSession = false },
        };

        _mockAuthorizationService
            .Setup(x => x.CheckPermissionAsync("api", "api:*:read"))
            .ReturnsAsync(true);

        _mockAlexaService
            .Setup(x => x.ProcessRequestAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        // Act
        var result = await _controller.HandleAlexaRequest(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<AlexaResponse>(okResult.Value);

        // Verify locale logging was called
        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>(
                        (v, t) => v.ToString()!.Contains("Alexa request locale: es-ES")
                    ),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.Once
        );
    }
}

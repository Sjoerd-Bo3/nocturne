using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V2;
using Nocturne.Core.Contracts.Notifications;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V2;

public class NotificationsControllerTests
{
    private readonly Mock<INotificationV2Service> _mockNotificationService;
    private readonly Mock<ILogger<NotificationsController>> _mockLogger;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _mockNotificationService = new Mock<INotificationV2Service>();
        _mockLogger = new Mock<ILogger<NotificationsController>>();
        _controller = new NotificationsController(
            _mockNotificationService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task SendLoopNotification_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "temp-basal",
            Message = "Temp Basal",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        var expectedResponse = new NotificationV2Response
        {
            Success = true,
            Message = "Loop notification processed successfully",
        };

        _mockNotificationService
            .Setup(s =>
                s.SendLoopNotificationAsync(
                    It.IsAny<LoopNotificationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendLoopNotification(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationV2Response>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Loop notification processed successfully", response.Message);
    }

    [Fact]
    public async Task SendLoopNotification_WithInvalidRequest_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoopNotificationRequest(); // Invalid request - missing required fields

        var expectedResponse = new NotificationV2Response
        {
            Success = false,
            Message = "Missing required 'type' field",
        };

        _mockNotificationService
            .Setup(s =>
                s.SendLoopNotificationAsync(
                    It.IsAny<LoopNotificationRequest>(),
                    It.IsAny<string>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.SendLoopNotification(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationV2Response>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Missing required 'type' field", response.Message);
    }

    [Fact]
    public async Task ProcessNotification_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new NotificationBase
        {
            Title = "Test Notification",
            Message = "This is a test notification",
        };

        var expectedResponse = new NotificationV2Response
        {
            Success = true,
            Message = "Notification processed successfully",
        };

        _mockNotificationService
            .Setup(s =>
                s.ProcessNotificationAsync(
                    It.IsAny<NotificationBase>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.ProcessNotification(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationV2Response>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Notification processed successfully", response.Message);
    }

    [Fact]
    public async Task GetNotificationStatus_ReturnsOkResult()
    {
        // Arrange
        var expectedResponse = new
        {
            status = "active",
            version = "v2",
            supported_types = new[] { "loop", "announcement", "alarm", "info" },
        };

        _mockNotificationService
            .Setup(s => s.GetNotificationStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetNotificationStatus(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task SendLoopNotification_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        LoopNotificationRequest? request = null;

        // Act
        var result = await _controller.SendLoopNotification(request!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationV2Response>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Request body is required", response.Message);
    }

    [Fact]
    public async Task ProcessNotification_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        NotificationBase? request = null;

        // Act
        var result = await _controller.ProcessNotification(request!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationV2Response>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Request body is required", response.Message);
    }
}

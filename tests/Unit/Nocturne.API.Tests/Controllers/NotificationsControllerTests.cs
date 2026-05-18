using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V1;
using Nocturne.Core.Contracts.Notifications;

namespace Nocturne.API.Tests.Controllers;

/// <summary>
/// Unit tests for NotificationsController V1 endpoints
/// Tests the Phase 8.1 notification endpoints with 1:1 legacy compatibility
/// </summary>
public class NotificationsControllerTests
{
    private readonly Mock<INotificationV1Service> _mockNotificationService;
    private readonly Mock<ILogger<NotificationsController>> _mockLogger;
    private readonly NotificationsController _controller;

    public NotificationsControllerTests()
    {
        _mockNotificationService = new Mock<INotificationV1Service>();
        _mockLogger = new Mock<ILogger<NotificationsController>>();
        _controller = new NotificationsController(
            _mockNotificationService.Object,
            _mockLogger.Object
        );

        // Setup HttpContext for remote IP address
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public async Task AckNotification_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new NotificationAckRequest
        {
            Level = 1,
            Group = "default",
            Time = 1800000, // 30 minutes
        };

        var expectedResponse = new NotificationAckResponse
        {
            Success = true,
            Message = "Acknowledged default - Level 1",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _mockNotificationService
            .Setup(s =>
                s.AckNotificationAsync(
                    It.IsAny<NotificationAckRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.AckNotification(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Acknowledged default - Level 1", response.Message);
        _mockNotificationService.Verify(
            s =>
                s.AckNotificationAsync(
                    It.IsAny<NotificationAckRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AckNotification_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        NotificationAckRequest? request = null;

        // Act
        var result = await _controller.AckNotification(request!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Request body is required", response.Message);
    }

    [Fact]
    public async Task AckNotification_WithInvalidLevel_ReturnsBadRequest()
    {
        // Arrange
        var request = new NotificationAckRequest
        {
            Level = 0, // Invalid level
            Group = "default",
        };

        // Act
        var result = await _controller.AckNotification(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Level must be 1 (WARN) or 2 (URGENT)", response.Message);
    }

    [Fact]
    public async Task AckNotification_WithLevel3_ReturnsBadRequest()
    {
        // Arrange
        var request = new NotificationAckRequest
        {
            Level = 3, // Invalid level (too high)
            Group = "default",
        };

        // Act
        var result = await _controller.AckNotification(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Level must be 1 (WARN) or 2 (URGENT)", response.Message);
    }

    [Fact]
    public async Task PushoverCallback_WithValidRequest_ReturnsOkResult()
    {
        // Arrange
        var request = new PushoverCallbackRequest
        {
            Receipt = "test-receipt-123",
            Status = 1,
            AcknowledgedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            AcknowledgedBy = "test-user",
        };

        var expectedResponse = new NotificationAckResponse
        {
            Success = true,
            Message = "Pushover callback processed successfully",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _mockNotificationService
            .Setup(s =>
                s.ProcessPushoverCallbackAsync(
                    It.IsAny<PushoverCallbackRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.PushoverCallback(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Pushover callback processed successfully", response.Message);
        _mockNotificationService.Verify(
            s =>
                s.ProcessPushoverCallbackAsync(
                    It.IsAny<PushoverCallbackRequest>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task PushoverCallback_WithNullRequest_ReturnsBadRequest()
    {
        // Arrange
        PushoverCallbackRequest? request = null;

        // Act
        var result = await _controller.PushoverCallback(request!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Request body is required", response.Message);
    }

    [Fact]
    public async Task GetAdminNotifies_ReturnsOkWithNotifications()
    {
        // Arrange
        var expectedResponse = new AdminNotifiesResponse
        {
            Status = 200,
            Message = new AdminNotifiesMessage
            {
                Notifies = new List<AdminNotification>
                {
                    new AdminNotification
                    {
                        Title = "Test Notification",
                        Message = "Test message",
                        Count = 1,
                        LastRecorded = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        Persistent = false,
                    },
                },
                NotifyCount = 1,
            },
        };
        _mockNotificationService
            .Setup(s => s.GetAdminNotifiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetAdminNotifies(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<AdminNotifiesResponse>(okResult.Value);
        Assert.Equal(200, response.Status);
        Assert.Equal(1, response.Message.NotifyCount);
        Assert.Single(response.Message.Notifies);
        _mockNotificationService.Verify(
            s => s.GetAdminNotifiesAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AddAdminNotification_WithValidNotification_ReturnsOkResult()
    {
        // Arrange
        var notification = new AdminNotification
        {
            Title = "Test Title",
            Message = "Test message",
            Persistent = false,
        };

        var expectedResponse = new NotificationAckResponse
        {
            Success = true,
            Message = "Admin notification added successfully",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _mockNotificationService
            .Setup(s =>
                s.AddAdminNotificationAsync(
                    It.IsAny<AdminNotification>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.AddAdminNotification(notification, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Admin notification added successfully", response.Message);
        _mockNotificationService.Verify(
            s =>
                s.AddAdminNotificationAsync(
                    It.IsAny<AdminNotification>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task AddAdminNotification_WithNullNotification_ReturnsBadRequest()
    {
        // Arrange
        AdminNotification? notification = null;

        // Act
        var result = await _controller.AddAdminNotification(notification!, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Request body is required", response.Message);
    }

    [Fact]
    public async Task ClearAllAdminNotifications_ReturnsOkResult()
    {
        // Arrange
        _mockNotificationService
            .Setup(s => s.ClearAllAdminNotificationsAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ClearAllAdminNotifications(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("All admin notifications cleared successfully", response.Message);
        _mockNotificationService.Verify(
            s => s.ClearAllAdminNotificationsAsync(It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task AckNotification_ServiceThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var request = new NotificationAckRequest { Level = 1, Group = "default" };

        _mockNotificationService
            .Setup(s =>
                s.AckNotificationAsync(
                    It.IsAny<NotificationAckRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new Exception("Database error"));

        // Act
        var result = await _controller.AckNotification(request, CancellationToken.None);

        // Assert
        var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusCodeResult.StatusCode);
        var response = Assert.IsType<NotificationAckResponse>(statusCodeResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Internal server error processing acknowledgment", response.Message);
    }

    [Fact]
    public async Task AckNotification_ServiceReturnsFailure_ReturnsBadRequest()
    {
        // Arrange
        var request = new NotificationAckRequest { Level = 1, Group = "default" };

        var failureResponse = new NotificationAckResponse
        {
            Success = false,
            Message = "Alarm already snoozed",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        };

        _mockNotificationService
            .Setup(s =>
                s.AckNotificationAsync(
                    It.IsAny<NotificationAckRequest>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(failureResponse);

        // Act
        var result = await _controller.AckNotification(request, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        var response = Assert.IsType<NotificationAckResponse>(badRequestResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Alarm already snoozed", response.Message);
    }
}

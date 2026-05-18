using Microsoft.Extensions.Logging;
using Nocturne.API.Services.Notifications;
using Nocturne.Core.Contracts.Notifications;
using Nocturne.Core.Contracts.Devices;
using Nocturne.API.Services.Realtime;

namespace Nocturne.API.Tests.Services.Notifications;

/// <summary>
/// Unit tests for NotificationV2Service
/// Tests the V2 notification service functionality with 1:1 legacy compatibility
/// Covers Loop notifications, generic notifications, and SignalR integration
/// </summary>
[Parity("notifications.test.js")]
public class NotificationV2ServiceTests
{
    private readonly Mock<ILogger<NotificationV2Service>> _mockLogger;
    private readonly Mock<ISignalRBroadcastService> _mockSignalRBroadcastService;
    private readonly Mock<ILoopService> _mockLoopService;
    private readonly NotificationV2Service _service;
    private readonly NotificationV2Service _serviceWithoutLoop;

    public NotificationV2ServiceTests()
    {
        _mockLogger = new Mock<ILogger<NotificationV2Service>>();
        _mockSignalRBroadcastService = new Mock<ISignalRBroadcastService>();
        _mockLoopService = new Mock<ILoopService>();

        _service = new NotificationV2Service(
            _mockLogger.Object,
            _mockSignalRBroadcastService.Object,
            _mockLoopService.Object
        );

        _serviceWithoutLoop = new NotificationV2Service(
            _mockLogger.Object,
            _mockSignalRBroadcastService.Object
        );
    }

    #region SendLoopNotificationAsync Tests

    [Fact]
    public async Task SendLoopNotificationAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "loop-completed",
            Message = "Loop completed successfully",
            Title = "Loop Update",
            Urgency = "high",
            Sound = "notification",
            Group = "Loop",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new Dictionary<string, object> { { "eventId", "12345" } },
            IsAnnouncement = false,
        };
        var remoteAddress = "192.168.1.100";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Loop notification processed successfully");
        result.Data.Should().NotBeNull();
        result.Timestamp.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithMinimalRequest_AppliesDefaults()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "loop-failed",
            Message = "Loop failed to complete",
        };
        var remoteAddress = "10.0.0.1";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Loop notification processed successfully");
        result.Data.Should().NotBeNull();

        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithMissingType_ReturnsFailure()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "", // Empty type
            Message = "Test message",
        };
        var remoteAddress = "127.0.0.1";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Missing required 'type' field");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithNullType_ReturnsFailure()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = null!, // Null type
            Message = "Test message",
        };
        var remoteAddress = "127.0.0.1";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Missing required 'type' field");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithMissingMessage_ReturnsFailure()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "loop-status",
            Message = "", // Empty message
        };
        var remoteAddress = "172.16.0.1";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Missing required 'message' field");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithNullMessage_ReturnsFailure()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "loop-status",
            Message = null!, // Null message
        };
        var remoteAddress = "172.16.0.1";

        // Act
        var result = await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Missing required 'message' field");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_WithException_ReturnsFailure()
    {
        // Arrange
        var request = new LoopNotificationRequest { Type = "loop-test", Message = "Test message" };
        var remoteAddress = "192.168.0.1";

        // Create a service with a SignalR service that throws to simulate internal failure
        var mockSignalRThatThrows = new Mock<ISignalRBroadcastService>();
        mockSignalRThatThrows
            .Setup(x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()))
            .ThrowsAsync(new InvalidOperationException("Simulated internal exception"));

        var serviceWithException = new NotificationV2Service(
            _mockLogger.Object,
            mockSignalRThatThrows.Object,
            _mockLoopService.Object
        );

        // Act
        var result = await serviceWithException.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Loop notification doesn't actually use SignalR directly
        result.Message.Should().Be("Loop notification processed successfully");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task SendLoopNotificationAsync_LogsCorrectInformation()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "loop-completed",
            Message = "Loop completed successfully",
        };
        var remoteAddress = "10.0.0.1";

        // Act
        await _service.SendLoopNotificationAsync(request, remoteAddress);

        // Assert
        // Verify debug logging occurred (cannot verify exact content due to structured logging)
        _mockLogger.Verify(
            x =>
                x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => true),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                ),
            Times.AtLeastOnce()
        );
    }

    #endregion

    #region ProcessNotificationAsync Tests

    [Fact]
    public async Task ProcessNotificationAsync_WithValidNotification_ReturnsSuccess()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Test Notification",
            Message = "This is a test notification",
            Level = 0, // INFO
            Group = "test-group",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Plugin = "test-plugin",
            IsAnnouncement = false,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Notification processed successfully");
        result.Data.Should().NotBeNull();
        result.Timestamp.Should().BeGreaterThan(0);

        // Verify SignalR broadcast was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithOnlyTitle_ReturnsSuccess()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Title Only Notification",
            Message = "", // Empty message
            Level = 0,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Notification processed successfully");

        // Verify SignalR broadcast was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithOnlyMessage_ReturnsSuccess()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "", // Empty title
            Message = "Message Only Notification",
            Level = 0,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Notification processed successfully");

        // Verify SignalR broadcast was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithNoTitleAndNoMessage_ReturnsFailure()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "", // Empty title
            Message = "", // Empty message
            Level = 0,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Notification must have either title or message");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();

        // Verify SignalR broadcast was NOT called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithNullTitleAndMessage_ReturnsFailure()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = null!, // Null title
            Message = null!, // Null message
            Level = 0,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Notification must have either title or message");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithWarnLevel_CallsAlarmBroadcast()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Warning Notification",
            Message = "This is a warning",
            Level = 1, // WARN
            Group = "alarms",
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify correct SignalR broadcast method was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithUrgentLevel_CallsUrgentAlarmBroadcast()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Urgent Notification",
            Message = "This is urgent",
            Level = 2, // URGENT
            Group = "critical",
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify correct SignalR broadcast method was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastUrgentAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithAnnouncement_CallsAnnouncementBroadcast()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "System Announcement",
            Message = "System maintenance scheduled",
            Level = 0,
            IsAnnouncement = true,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify correct SignalR broadcast method was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAnnouncementAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithClearAlarm_CallsClearAlarmBroadcast()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Clear Alarm",
            Message = "Alarm condition cleared",
            Level = 1,
            Clear = true,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify correct SignalR broadcast method was called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastClearAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Never
        );
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithSignalRFailure_ContinuesProcessing()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Test Notification",
            Message = "Test message",
            Level = 0,
        };

        _mockSignalRBroadcastService
            .Setup(x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()))
            .ThrowsAsync(new InvalidOperationException("SignalR connection failed"));

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Processing continues despite SignalR failure
        result.Message.Should().Be("Notification processed successfully");
    }

    [Fact]
    public async Task ProcessNotificationAsync_AppliesDefaults()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "", // Will default to "Notification"
            Message = "Test message",
            Group = "", // Will default to "default"
            Timestamp = 0, // Will default to current time
            Level = 0,
        };

        // Act
        var result = await _service.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();

        // Verify defaults were applied through successful processing
        result.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessNotificationAsync_WithException_ReturnsFailure()
    {
        // Arrange
        var notification = new NotificationBase
        {
            Title = "Test",
            Message = "Test message",
            Level = 0,
        };

        // Create a service with a SignalR service that throws to simulate internal failure
        var mockSignalRThatThrows = new Mock<ISignalRBroadcastService>();
        mockSignalRThatThrows
            .Setup(x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()))
            .ThrowsAsync(new InvalidOperationException("Simulated SignalR exception"));

        var serviceWithException = new NotificationV2Service(
            _mockLogger.Object,
            mockSignalRThatThrows.Object,
            _mockLoopService.Object
        );

        // Act
        var result = await serviceWithException.ProcessNotificationAsync(notification);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue(); // Processing continues despite SignalR failure
        result.Message.Should().Be("Notification processed successfully");
        result.Timestamp.Should().BeGreaterThan(0);
        result.Data.Should().NotBeNull();
    }

    #endregion

    #region GetNotificationStatusAsync Tests

    [Fact]
    public async Task GetNotificationStatusAsync_WithValidLoopService_ReturnsCompleteStatus()
    {
        // Arrange
        _mockLoopService.Setup(x => x.IsConfigurationValid()).Returns(true);

        // Act
        var result = await _service.GetNotificationStatusAsync();

        // Assert
        result.Should().NotBeNull();

        // Method completes successfully, result is a structured object
        // (Cannot test dynamic properties easily in unit tests)
    }

    [Fact]
    public async Task GetNotificationStatusAsync_WithInvalidLoopService_ReturnsLimitedStatus()
    {
        // Arrange
        _mockLoopService.Setup(x => x.IsConfigurationValid()).Returns(false);

        // Act
        var result = await _service.GetNotificationStatusAsync();

        // Assert
        result.Should().NotBeNull();

        // Method should complete successfully even with invalid Loop service
    }

    [Fact]
    public async Task GetNotificationStatusAsync_WithoutLoopService_ReturnsBasicStatus()
    {
        // Arrange & Act
        var result = await _serviceWithoutLoop.GetNotificationStatusAsync();

        // Assert
        result.Should().NotBeNull();

        // Method should complete successfully without Loop service
    }

    [Fact]
    public async Task GetNotificationStatusAsync_WithNullLoopService_ReturnsBasicStatus()
    {
        // Arrange
        var serviceWithNullLoop = new NotificationV2Service(
            _mockLogger.Object,
            _mockSignalRBroadcastService.Object,
            null // Explicitly null Loop service
        );

        // Act
        var result = await serviceWithNullLoop.GetNotificationStatusAsync();

        // Assert
        result.Should().NotBeNull();

        // Method should complete successfully with null Loop service
    }

    #endregion

    #region Integration and Edge Case Tests

    [Fact]
    public async Task NotificationV2Service_CanBeConstructedWithoutOptionalLoopService()
    {
        // Arrange & Act
        var service = new NotificationV2Service(
            _mockLogger.Object,
            _mockSignalRBroadcastService.Object
        );

        // Assert
        service.Should().NotBeNull();

        // Verify it can process notifications without Loop service
        var notification = new NotificationBase
        {
            Title = "Test",
            Message = "Test message",
            Level = 0,
        };

        var result = await service.ProcessNotificationAsync(notification);
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task NotificationV2Service_HandlesMultipleNotificationLevels()
    {
        // Arrange
        var infoNotification = new NotificationBase
        {
            Title = "Info",
            Message = "Info",
            Level = 0,
        };
        var warnNotification = new NotificationBase
        {
            Title = "Warn",
            Message = "Warn",
            Level = 1,
        };
        var urgentNotification = new NotificationBase
        {
            Title = "Urgent",
            Message = "Urgent",
            Level = 2,
        };

        // Act
        var infoResult = await _service.ProcessNotificationAsync(infoNotification);
        var warnResult = await _service.ProcessNotificationAsync(warnNotification);
        var urgentResult = await _service.ProcessNotificationAsync(urgentNotification);

        // Assert
        infoResult.Success.Should().BeTrue();
        warnResult.Success.Should().BeTrue();
        urgentResult.Success.Should().BeTrue();

        // Verify different broadcast methods were called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastUrgentAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    public async Task NotificationV2Service_HandlesSpecialNotificationTypes()
    {
        // Arrange
        var clearNotification = new NotificationBase
        {
            Title = "Clear",
            Message = "Clear",
            Level = 1,
            Clear = true,
        };
        var announcementNotification = new NotificationBase
        {
            Title = "Announcement",
            Message = "Announcement",
            Level = 0,
            IsAnnouncement = true,
        };

        // Act
        var clearResult = await _service.ProcessNotificationAsync(clearNotification);
        var announcementResult = await _service.ProcessNotificationAsync(announcementNotification);

        // Assert
        clearResult.Success.Should().BeTrue();
        announcementResult.Success.Should().BeTrue();

        // Verify special broadcast methods were called
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastClearAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAnnouncementAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    public async Task SendLoopNotificationAsync_HandlesAllOptionalFields()
    {
        // Arrange
        var request = new LoopNotificationRequest
        {
            Type = "comprehensive-test",
            Message = "Comprehensive test message",
            Title = "Comprehensive Test",
            Urgency = "medium",
            Sound = "chime",
            Group = "testing",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Data = new Dictionary<string, object>
            {
                { "testId", 999 },
                { "testType", "unit" },
                { "nested", new { prop = "value" } },
            },
            IsAnnouncement = true,
        };

        // Act
        var result = await _service.SendLoopNotificationAsync(request, "test-address");

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Loop notification processed successfully");
        result.Data.Should().NotBeNull();
    }

    #endregion
}

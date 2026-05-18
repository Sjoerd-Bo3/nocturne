using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Core.Models;
using Xunit;
using Nocturne.API.Tests.Services.Glucose;

namespace Nocturne.API.Tests.Services.Legacy;

/// <summary>
/// Tests for simple alarms functionality with 1:1 legacy compatibility
/// Based on legacy simplealarms.test.js
/// </summary>
[Parity("simplealarms.test.js")]
public class SimpleAlarmsTests
{
    private readonly Mock<ILogger<SimpleAlarmsService>> _mockLogger;
    private readonly SimpleAlarmsService _simpleAlarmsService;

    public SimpleAlarmsTests()
    {
        _mockLogger = new Mock<ILogger<SimpleAlarmsService>>();
        _simpleAlarmsService = new SimpleAlarmsService(_mockLogger.Object);
    }

    [Fact]
    public void CheckAlarms_ShouldNotTriggerWhenInRange()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Mgdl = 100 },
        };
        var settings = CreateDefaultAlarmSettings();

        // Act
        var notifications = _simpleAlarmsService.CheckAlarms(entries, settings);

        // Assert
        Assert.Empty(notifications);
    }

    [Fact]
    public void CheckAlarms_ShouldTriggerWarningWhenAboveTarget()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var fiveMinutesAgo = now - (5 * 60 * 1000);

        var entries = new List<Entry>
        {
            new() { Mills = fiveMinutesAgo, Mgdl = 171 },
            new() { Mills = now, Mgdl = 181 },
        };
        var settings = CreateDefaultAlarmSettings();

        // Act
        var notifications = _simpleAlarmsService.CheckAlarms(entries, settings);

        // Assert
        Assert.Single(notifications);
        var notification = notifications.First();
        Assert.Equal(Levels.WARN, notification.Level);
        Assert.Equal("BG Now: 181 +10 mg/dl", notification.Message);
    }

    [Fact]
    public void CheckAlarms_ShouldTriggerUrgentWhenReallyHigh()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Mgdl = 400 },
        };
        var settings = CreateDefaultAlarmSettings();

        // Act
        var notifications = _simpleAlarmsService.CheckAlarms(entries, settings);

        // Assert
        Assert.Single(notifications);
        var notification = notifications.First();
        Assert.Equal(Levels.URGENT, notification.Level);
    }

    [Fact]
    public void CheckAlarms_ShouldTriggerWarningWhenBelowTarget()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Mgdl = 70 },
        };
        var settings = CreateDefaultAlarmSettings();

        // Act
        var notifications = _simpleAlarmsService.CheckAlarms(entries, settings);

        // Assert
        Assert.Single(notifications);
        var notification = notifications.First();
        Assert.Equal(Levels.WARN, notification.Level);
    }

    [Fact]
    public void CheckAlarms_ShouldTriggerUrgentWhenReallyLow()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), Mgdl = 40 },
        };
        var settings = CreateDefaultAlarmSettings();

        // Act
        var notifications = _simpleAlarmsService.CheckAlarms(entries, settings);

        // Assert
        Assert.Single(notifications);
        var notification = notifications.First();
        Assert.Equal(Levels.URGENT, notification.Level);
    }

    private static AlarmSettings CreateDefaultAlarmSettings()
    {
        return new AlarmSettings
        {
            High = 180,
            UrgentHigh = 300,
            Low = 80,
            UrgentLow = 55,
            Units = "mg/dl",
        };
    }
}

/// <summary>
/// Service for simple threshold-based alarms with 1:1 legacy compatibility
/// </summary>
public class SimpleAlarmsService
{
    private readonly ILogger<SimpleAlarmsService> _logger;

    public SimpleAlarmsService(ILogger<SimpleAlarmsService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Check for alarm conditions based on current glucose readings
    /// </summary>
    public List<NotificationBase> CheckAlarms(IList<Entry> entries, AlarmSettings settings)
    {
        var notifications = new List<NotificationBase>();

        if (!entries.Any())
            return notifications;

        var currentEntry = entries.OrderByDescending(e => e.Mills).First();
        var currentBg = currentEntry.Mgdl;

        // Calculate delta if we have multiple entries
        string deltaText = "";
        if (entries.Count > 1)
        {
            var previousEntry = entries.OrderByDescending(e => e.Mills).Skip(1).First();
            var delta = currentBg - previousEntry.Mgdl;
            deltaText = $" {(delta >= 0 ? "+" : "")}{delta}";
        }

        var message = $"BG Now: {currentBg}{deltaText} {settings.Units}";

        // Check for urgent high
        if (currentBg >= settings.UrgentHigh)
        {
            notifications.Add(
                new NotificationBase
                {
                    Level = Levels.URGENT,
                    Title = "Urgent High",
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }
        // Check for high warning
        else if (currentBg >= settings.High)
        {
            notifications.Add(
                new NotificationBase
                {
                    Level = Levels.WARN,
                    Title = "High",
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }
        // Check for urgent low
        else if (currentBg <= settings.UrgentLow)
        {
            notifications.Add(
                new NotificationBase
                {
                    Level = Levels.URGENT,
                    Title = "Urgent Low",
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }
        // Check for low warning
        else if (currentBg <= settings.Low)
        {
            notifications.Add(
                new NotificationBase
                {
                    Level = Levels.WARN,
                    Title = "Low",
                    Message = message,
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                }
            );
        }

        return notifications;
    }
}

/// <summary>
/// Alarm threshold settings
/// </summary>
public class AlarmSettings
{
    public int High { get; set; } = 180;
    public int UrgentHigh { get; set; } = 300;
    public int Low { get; set; } = 80;
    public int UrgentLow { get; set; } = 55;
    public string Units { get; set; } = "mg/dl";
}

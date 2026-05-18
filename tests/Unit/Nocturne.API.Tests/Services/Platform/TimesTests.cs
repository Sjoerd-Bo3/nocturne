using Xunit;

namespace Nocturne.API.Tests.Services.Platform;

/// <summary>
/// Tests for time conversion utilities with 1:1 legacy compatibility
/// Based on legacy times.test.js
/// </summary>
[Parity("times.test.js")]
public class TimesTests
{
    [Fact]
    public void Hour_ShouldConvertToMinutesSecondsAndMilliseconds()
    {
        // Arrange & Act
        var hour = TimeUtils.Hour();

        // Assert
        Assert.Equal(60, hour.Mins);
        Assert.Equal(3600, hour.Secs);
        Assert.Equal(3600000, hour.Msecs);
    }

    [Fact]
    public void Hours_ShouldConvertMultipleHoursToMinutesSecondsAndMilliseconds()
    {
        // Arrange & Act
        var threeHours = TimeUtils.Hours(3);

        // Assert
        Assert.Equal(180, threeHours.Mins);
        Assert.Equal(10800, threeHours.Secs);
        Assert.Equal(10800000, threeHours.Msecs);
    }

    [Fact]
    public void Min_ShouldConvertToSecondsAndMilliseconds()
    {
        // Arrange & Act
        var minute = TimeUtils.Min();

        // Assert
        Assert.Equal(60, minute.Secs);
        Assert.Equal(60000, minute.Msecs);
    }

    [Fact]
    public void Mins_ShouldConvertMultipleMinutesToSecondsAndMilliseconds()
    {
        // Arrange & Act
        var twoMinutes = TimeUtils.Mins(2);

        // Assert
        Assert.Equal(120, twoMinutes.Secs);
        Assert.Equal(120000, twoMinutes.Msecs);
    }

    [Fact]
    public void Sec_ShouldConvertToMilliseconds()
    {
        // Arrange & Act
        var second = TimeUtils.Sec();

        // Assert
        Assert.Equal(1000, second.Msecs);
    }

    [Fact]
    public void Secs_ShouldConvertMultipleSecondsToMilliseconds()
    {
        // Arrange & Act
        var fifteenSeconds = TimeUtils.Secs(15);

        // Assert
        Assert.Equal(15000, fifteenSeconds.Msecs);
    }
}

/// <summary>
/// Time conversion utilities for 1:1 legacy compatibility
/// </summary>
public static class TimeUtils
{
    /// <summary>
    /// Get time conversions for one hour
    /// </summary>
    public static TimeConversion Hour() => Hours(1);

    /// <summary>
    /// Get time conversions for multiple hours
    /// </summary>
    public static TimeConversion Hours(int hours) => new(hours * 60, hours * 3600, hours * 3600000);

    /// <summary>
    /// Get time conversions for one minute
    /// </summary>
    public static TimeConversion Min() => Mins(1);

    /// <summary>
    /// Get time conversions for multiple minutes
    /// </summary>
    public static TimeConversion Mins(int minutes) => new(minutes, minutes * 60, minutes * 60000);

    /// <summary>
    /// Get time conversions for one second
    /// </summary>
    public static TimeConversion Sec() => Secs(1);

    /// <summary>
    /// Get time conversions for multiple seconds
    /// </summary>
    public static TimeConversion Secs(int seconds) => new(0, seconds, seconds * 1000);
}

/// <summary>
/// Time conversion result
/// </summary>
public record TimeConversion(int Mins, int Secs, int Msecs);

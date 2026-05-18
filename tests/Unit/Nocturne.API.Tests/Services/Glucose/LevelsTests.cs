using Xunit;

namespace Nocturne.API.Tests.Services.Glucose;

/// <summary>
/// Tests for notification levels constants with 1:1 legacy compatibility
/// Based on legacy levels.test.js
/// </summary>
[Parity("levels.test.js")]
public class LevelsTests
{
    [Fact]
    public void Levels_ShouldHaveCorrectConstantValues()
    {
        // Arrange & Act & Assert
        Assert.Equal(2, Levels.URGENT);
        Assert.Equal(1, Levels.WARN);
        Assert.Equal(0, Levels.INFO);
        Assert.Equal(-1, Levels.LOW);
        Assert.Equal(-2, Levels.LOWEST);
        Assert.Equal(-3, Levels.NONE);
    }

    [Fact]
    public void ToDisplay_ShouldConvertLevelsToDisplayText()
    {
        // Arrange & Act & Assert
        Assert.Equal("Urgent", Levels.ToDisplay(Levels.URGENT));
        Assert.Equal("Warning", Levels.ToDisplay(Levels.WARN));
        Assert.Equal("Info", Levels.ToDisplay(Levels.INFO));
        Assert.Equal("Low", Levels.ToDisplay(Levels.LOW));
        Assert.Equal("Lowest", Levels.ToDisplay(Levels.LOWEST));
        Assert.Equal("None", Levels.ToDisplay(Levels.NONE));
        Assert.Equal("Unknown", Levels.ToDisplay(42));
        Assert.Equal("Unknown", Levels.ToDisplay(99));
    }

    [Fact]
    public void ToLowerCase_ShouldConvertLevelsToLowerCaseText()
    {
        // Arrange & Act & Assert
        Assert.Equal("urgent", Levels.ToLowerCase(Levels.URGENT));
        Assert.Equal("warning", Levels.ToLowerCase(Levels.WARN));
        Assert.Equal("info", Levels.ToLowerCase(Levels.INFO));
        Assert.Equal("low", Levels.ToLowerCase(Levels.LOW));
        Assert.Equal("lowest", Levels.ToLowerCase(Levels.LOWEST));
        Assert.Equal("none", Levels.ToLowerCase(Levels.NONE));
        Assert.Equal("unknown", Levels.ToLowerCase(42));
        Assert.Equal("unknown", Levels.ToLowerCase(99));
    }
}

/// <summary>
/// Static class containing notification level constants for 1:1 legacy compatibility
/// </summary>
public static class Levels
{
    public const int URGENT = 2;
    public const int WARN = 1;
    public const int INFO = 0;
    public const int LOW = -1;
    public const int LOWEST = -2;
    public const int NONE = -3;

    /// <summary>
    /// Convert level constant to display text
    /// </summary>
    public static string ToDisplay(int level)
    {
        return level switch
        {
            URGENT => "Urgent",
            WARN => "Warning",
            INFO => "Info",
            LOW => "Low",
            LOWEST => "Lowest",
            NONE => "None",
            _ => "Unknown",
        };
    }

    /// <summary>
    /// Convert level constant to lowercase text
    /// </summary>
    public static string ToLowerCase(int level)
    {
        return level switch
        {
            URGENT => "urgent",
            WARN => "warning",
            INFO => "info",
            LOW => "low",
            LOWEST => "lowest",
            NONE => "none",
            _ => "unknown",
        };
    }
}

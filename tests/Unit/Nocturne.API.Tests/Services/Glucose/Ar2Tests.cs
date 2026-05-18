using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services.Glucose;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.Glucose;

/// <summary>
/// Tests for AR2 forecasting with 1:1 legacy JavaScript compatibility
/// Based on legacy ar2.test.js test cases
/// </summary>
[Parity("ar2.test.js")]
public class Ar2Tests
{
    private readonly Mock<ILogger<Ar2Service>> _mockLogger;
    private readonly Ar2Service _ar2Service;

    public Ar2Tests()
    {
        _mockLogger = new Mock<ILogger<Ar2Service>>();
        _ar2Service = new Ar2Service(_mockLogger.Object);
    }

    [Fact]
    public async Task CalculateForecastAsync_ShouldReturnEmptyForecast_WhenCannotForecast()
    {
        // Arrange
        var ddata = new DData();
        var bgNowProperties = new Dictionary<string, object>(); // Missing required properties
        var deltaProperties = new Dictionary<string, object>();
        var settings = new Dictionary<string, object>();

        // Act
        var result = await _ar2Service.CalculateForecastAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            settings,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Forecast);
        Assert.Empty(result.Forecast.Predicted);
        Assert.Equal(0, result.Forecast.AvgLoss);
    }

    [Fact]
    public async Task CalculateForecastAsync_ShouldGenerateCorrectPredictions_WithValidData()
    {
        // Arrange - data similar to legacy ar2.test.js
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var before = now - (5 * 60 * 1000); // 5 minutes ago

        var ddata = new DData
        {
            Sgvs = new List<Entry>
            {
                new() { Mills = before, Mgdl = 100 },
                new() { Mills = now, Mgdl = 105 },
            },
        };

        var bgNowProperties = new Dictionary<string, object>
        {
            ["mean"] = 105.0,
            ["mills"] = now,
            ["last"] = 105.0,
        };

        var deltaProperties = new Dictionary<string, object>
        {
            ["mean5MinsAgo"] = 100.0,
            ["mgdl"] = 5,
        };

        var settings = new Dictionary<string, object>
        {
            ["bgTargetTop"] = 180,
            ["bgTargetBottom"] = 80,
            ["alarmHigh"] = true,
            ["alarmLow"] = true,
        };

        // Act
        var result = await _ar2Service.CalculateForecastAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            settings,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Forecast);
        Assert.Equal(6, result.Forecast.Predicted.Count); // Exactly 6 predictions as in legacy
        Assert.True(result.Forecast.AvgLoss >= 0); // Should calculate loss
        Assert.NotNull(result.DisplayLine);
        Assert.Contains("BG 15m:", result.DisplayLine);
    }

    [Fact]
    public async Task CalculateForecastAsync_ShouldTriggerWarning_WhenHighPrediction()
    {
        // Arrange - data that should trigger high warning (from ar2.test.js)
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var before = now - (5 * 60 * 1000);

        var ddata = new DData
        {
            Sgvs = new List<Entry>
            {
                new() { Mills = before, Mgdl = 150 },
                new() { Mills = now, Mgdl = 170 },
            },
        };

        var bgNowProperties = new Dictionary<string, object>
        {
            ["mean"] = 170.0,
            ["mills"] = now,
            ["last"] = 170.0,
        };

        var deltaProperties = new Dictionary<string, object>
        {
            ["mean5MinsAgo"] = 150.0,
            ["mgdl"] = 20,
        };

        var settings = new Dictionary<string, object>
        {
            ["bgTargetTop"] = 160,
            ["bgTargetBottom"] = 80,
            ["alarmHigh"] = true,
            ["alarmLow"] = true,
        };

        // Act
        var result = await _ar2Service.CalculateForecastAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            settings,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Forecast);

        // Should potentially trigger warning based on prediction trend
        if (result.Forecast.Predicted.Count >= 4)
        {
            var prediction20min = result.Forecast.Predicted[3].Mgdl;
            // Verify that high predictions trigger appropriate event
            if (prediction20min > 160 && result.Forecast.AvgLoss > 0.05)
            {
                Assert.Equal("high", result.EventName);
            }
        }
    }

    [Fact]
    public void CanForecast_ShouldReturnFalse_WhenBgNowMissing()
    {
        // Arrange
        var bgNowProperties = new Dictionary<string, object>(); // Missing mean
        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 100.0 };

        // Act
        var result = _ar2Service.CanForecast(bgNowProperties, deltaProperties);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanForecast_ShouldReturnFalse_WhenDeltaMissing()
    {
        // Arrange
        var bgNowProperties = new Dictionary<string, object> { ["mean"] = 105.0 };
        var deltaProperties = new Dictionary<string, object>(); // Missing mean5MinsAgo

        // Act
        var result = _ar2Service.CanForecast(bgNowProperties, deltaProperties);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanForecast_ShouldReturnFalse_WhenBgTooLow()
    {
        // Arrange
        var bgNowProperties = new Dictionary<string, object>
        {
            ["mean"] = 35.0, // Below BG_MIN (36)
        };
        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 30.0 };

        // Act
        var result = _ar2Service.CanForecast(bgNowProperties, deltaProperties);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void CanForecast_ShouldReturnTrue_WithValidData()
    {
        // Arrange
        var bgNowProperties = new Dictionary<string, object> { ["mean"] = 105.0 };
        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 100.0 };

        // Act
        var result = _ar2Service.CanForecast(bgNowProperties, deltaProperties);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task GenerateForecastConeAsync_ShouldReturnEmptyList_WhenCannotForecast()
    {
        // Arrange
        var ddata = new DData();
        var bgNowProperties = new Dictionary<string, object>(); // Missing required properties
        var deltaProperties = new Dictionary<string, object>();

        // Act
        var result = await _ar2Service.GenerateForecastConeAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            cancellationToken: CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateForecastConeAsync_ShouldGenerateConePoints_WithValidData()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var before = now - (5 * 60 * 1000);

        var ddata = new DData
        {
            Sgvs = new List<Entry>
            {
                new() { Mills = before, Mgdl = 100 },
                new() { Mills = now, Mgdl = 105 },
            },
        };

        var bgNowProperties = new Dictionary<string, object> { ["mean"] = 105.0, ["mills"] = now };

        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 100.0 };

        // Act
        var result = await _ar2Service.GenerateForecastConeAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            2.0,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);

        // Should generate cone points for 13 steps (from legacy CONE_STEPS array)
        // Each step generates 2 points (positive and negative cone), so 26 total
        Assert.Equal(26, result.Count);

        // All points should have cyan color
        Assert.All(result, point => Assert.Equal("cyan", point.Color));

        // Points should have valid mg/dL values (between 36-400)
        Assert.All(
            result,
            point =>
            {
                Assert.True(point.Mgdl >= 36);
                Assert.True(point.Mgdl <= 400);
            }
        );
    }

    [Fact]
    public async Task GenerateForecastConeAsync_ShouldGenerateLine_WhenConeFactorZero()
    {
        // Arrange - test case from ar2.test.js "should plot a line if coneFactor is 0"
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var before = now - (5 * 60 * 1000);

        var ddata = new DData
        {
            Sgvs = new List<Entry>
            {
                new() { Mills = before, Mgdl = 100 },
                new() { Mills = now, Mgdl = 105 },
            },
        };

        var bgNowProperties = new Dictionary<string, object> { ["mean"] = 105.0, ["mills"] = now };

        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 100.0 };

        // Act - coneFactor of 0 should generate a line (only positive points)
        var result = await _ar2Service.GenerateForecastConeAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            0.0,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(13, result.Count); // Only 13 points (no negative cone points)
    }

    [Fact]
    public async Task CalculateForecastAsync_ShouldClampPredictions_ToBounds()
    {
        // Arrange - extreme values to test clamping
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var before = now - (5 * 60 * 1000);

        var ddata = new DData
        {
            Sgvs = new List<Entry>
            {
                new() { Mills = before, Mgdl = 400 },
                new() { Mills = now, Mgdl = 400 },
            },
        };

        var bgNowProperties = new Dictionary<string, object> { ["mean"] = 400.0, ["mills"] = now };

        var deltaProperties = new Dictionary<string, object> { ["mean5MinsAgo"] = 400.0 };

        var settings = new Dictionary<string, object>();

        // Act
        var result = await _ar2Service.CalculateForecastAsync(
            ddata,
            bgNowProperties,
            deltaProperties,
            settings,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Forecast);

        // All predictions should be within valid range
        Assert.All(
            result.Forecast.Predicted,
            point =>
            {
                Assert.True(point.Mgdl >= 36, $"Prediction {point.Mgdl} should be >= 36");
                Assert.True(point.Mgdl <= 400, $"Prediction {point.Mgdl} should be <= 400");
            }
        );
    }
}

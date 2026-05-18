using Nocturne.Core.Contracts.Devices;

namespace Nocturne.API.Tests.Services.Devices;

/// <summary>
/// Unit tests for oref model types.
/// These tests verify the C# model types match the expected structure for Rust WASM interop.
/// </summary>
public class OrefModelsTests
{
    [Fact]
    public void OrefProfile_DefaultValues_MatchLegacyDefaults()
    {
        // Arrange & Act
        var profile = new OrefProfile();

        // Assert - verify defaults match oref defaults
        Assert.Equal(3.0, profile.Dia);
        Assert.Equal(10.0, profile.MaxIob);
        Assert.Equal(4.0, profile.MaxBasal);
        Assert.Equal(100.0, profile.MinBg);
        Assert.Equal(120.0, profile.MaxBg);
        Assert.Equal(50.0, profile.Sens);
        Assert.Equal(10.0, profile.CarbRatio);
        Assert.Equal("rapid-acting", profile.Curve);
        Assert.Equal(75, profile.Peak);
        Assert.Equal(0.7, profile.AutosensMin);
        Assert.Equal(1.2, profile.AutosensMax);
        Assert.Equal(8.0, profile.Min5mCarbimpact);
        Assert.Equal(120.0, profile.MaxCob);
        Assert.Equal(6.0, profile.MaxMealAbsorptionTime);
    }

    [Fact]
    public void OrefTreatment_Bolus_CreatesCorrectTreatment()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var insulinUnits = 2.5;

        // Act
        var treatment = OrefTreatment.Bolus(insulinUnits, timestamp);

        // Assert
        Assert.Equal(insulinUnits, treatment.Insulin);
        Assert.Equal(timestamp.ToUnixTimeMilliseconds(), treatment.Date);
        Assert.Equal("Bolus", treatment.EventType);
        Assert.NotNull(treatment.Timestamp);
        Assert.NotNull(treatment.StartedAt);
    }

    [Fact]
    public void OrefTreatment_TempBasal_CreatesCorrectTreatment()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var rate = 1.5;
        var duration = 30.0;

        // Act
        var treatment = OrefTreatment.TempBasal(rate, duration, timestamp);

        // Assert
        Assert.Equal(rate, treatment.Rate);
        Assert.Equal(duration, treatment.Duration);
        Assert.Equal(timestamp.ToUnixTimeMilliseconds(), treatment.Date);
        Assert.Equal("TempBasal", treatment.EventType);
    }

    [Fact]
    public void OrefTreatment_CarbEntry_CreatesCorrectTreatment()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var carbs = 45.0;

        // Act
        var treatment = OrefTreatment.CarbEntry(carbs, timestamp);

        // Assert
        Assert.Equal(carbs, treatment.Carbs);
        Assert.Equal(carbs, treatment.NsCarbs);
        Assert.Equal(timestamp.ToUnixTimeMilliseconds(), treatment.Date);
        Assert.Equal("Carbs", treatment.EventType);
    }

    [Fact]
    public void OrefCurrentTemp_None_ReturnsZeroedTemp()
    {
        // Act
        var temp = OrefCurrentTemp.None;

        // Assert
        Assert.Equal(0, temp.Duration);
        Assert.Equal(0, temp.Rate);
        Assert.Equal("absolute", temp.Temp);
    }

    [Fact]
    public void OrefAutosensResult_Default_HasRatioOfOne()
    {
        // Arrange & Act
        var result = new OrefAutosensResult();

        // Assert
        Assert.Equal(1.0, result.Ratio);
    }

    [Fact]
    public void OrefDetermineBasalResult_HasSmb_ReturnsTrueWhenUnitsSet()
    {
        // Arrange
        var result = new OrefDetermineBasalResult { Units = 0.5 };

        // Assert
        Assert.True(result.HasSmb);
    }

    [Fact]
    public void OrefDetermineBasalResult_HasSmb_ReturnsFalseWhenNoUnits()
    {
        // Arrange
        var result = new OrefDetermineBasalResult();

        // Assert
        Assert.False(result.HasSmb);
    }

    [Fact]
    public void OrefDetermineBasalResult_HasTemp_ReturnsTrueWhenRateAndDurationSet()
    {
        // Arrange
        var result = new OrefDetermineBasalResult { Rate = 1.5, Duration = 30 };

        // Assert
        Assert.True(result.HasTemp);
    }

    [Fact]
    public void OrefDetermineBasalResult_HasError_ReturnsTrueWhenErrorSet()
    {
        // Arrange
        var result = new OrefDetermineBasalResult { Error = "Test error" };

        // Assert
        Assert.True(result.HasError);
    }

    [Fact]
    public void OrefDetermineBasalInputs_RequiredProperties_AreEnforced()
    {
        // Arrange & Act
        var inputs = new OrefDetermineBasalInputs
        {
            GlucoseStatus = new OrefGlucoseStatus { Glucose = 120 },
            CurrentTemp = OrefCurrentTemp.None,
            IobData = new OrefIobResult { Iob = 2.5 },
            Profile = new OrefProfile(),
        };

        // Assert
        Assert.NotNull(inputs.GlucoseStatus);
        Assert.NotNull(inputs.CurrentTemp);
        Assert.NotNull(inputs.IobData);
        Assert.NotNull(inputs.Profile);
    }
}

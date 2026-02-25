using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Tests.Mappers;

/// <summary>
/// Unit tests for TreatmentStateSpanMapper
/// </summary>
[Trait("Category", "Unit")]
public class TreatmentStateSpanMapperTests
{
    #region IsTempBasalTreatment Tests

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Temp Basal", true)]
    [InlineData("temp basal", true)]
    [InlineData("TEMP BASAL", true)]
    [InlineData("Temp Basal Start", true)]
    [InlineData("temp basal start", true)]
    [InlineData("TEMP BASAL START", true)]
    [InlineData("TempBasal", true)]
    [InlineData("tempbasal", true)]
    [InlineData("TEMPBASAL", true)]
    [InlineData("Meal Bolus", false)]
    [InlineData("Correction Bolus", false)]
    [InlineData("BG Check", false)]
    [InlineData("Site Change", false)]
    [InlineData("Profile Switch", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsTempBasalTreatment_VariousEventTypes_ReturnsCorrectly(string? eventType, bool expected)
    {
        // Arrange
        var treatment = new Treatment
        {
            Id = "test-treatment",
            EventType = eventType,
            Mills = 1700000000000
        };

        // Act
        var result = TreatmentStateSpanMapper.IsTempBasalTreatment(treatment);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsTempBasalTreatment_NullTreatment_ReturnsFalse()
    {
        // Act
        var result = TreatmentStateSpanMapper.IsTempBasalTreatment(null!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}

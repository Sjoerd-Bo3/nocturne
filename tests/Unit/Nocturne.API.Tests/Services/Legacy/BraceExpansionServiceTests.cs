using FluentAssertions;
using Nocturne.API.Services.Legacy;
using Nocturne.Core.Contracts.Legacy;
using Xunit;

namespace Nocturne.API.Tests.Services.Legacy;

/// <summary>
/// Unit tests for the BraceExpansionService
/// Tests bash-style brace expansion functionality for time pattern matching
/// </summary>
public class BraceExpansionServiceTests
{
    private readonly IBraceExpansionService _service;

    public BraceExpansionServiceTests()
    {
        _service = new BraceExpansionService();
    }

    [Fact]
    public void ExpandBraces_WithNumericRange_ShouldExpandCorrectly()
    {
        // Arrange
        var pattern = "20{14..16}";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().Equal("2014", "2015", "2016");
    }

    [Fact]
    public void ExpandBraces_WithZeroPaddedRange_ShouldMaintainPadding()
    {
        // Arrange
        var pattern = "T{13..15}:{00..02}";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result
            .Should()
            .Equal(
                "T13:00",
                "T13:01",
                "T13:02",
                "T14:00",
                "T14:01",
                "T14:02",
                "T15:00",
                "T15:01",
                "T15:02"
            );
    }

    [Fact]
    public void ExpandBraces_WithSequencePattern_ShouldExpandCorrectly()
    {
        // Arrange
        var pattern = "{sgv,mbg,cal}";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().Equal("sgv", "mbg", "cal");
    }

    [Fact]
    public void ExpandBraces_WithComplexPattern_ShouldExpandRecursively()
    {
        // Arrange
        var pattern = "20{14..15}-{04,05}";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().Equal("2014-04", "2014-05", "2015-04", "2015-05");
    }

    [Fact]
    public void ExpandBraces_WithNoBraces_ShouldReturnOriginal()
    {
        // Arrange
        var pattern = "2015-04-01";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().Equal("2015-04-01");
    }

    [Fact]
    public void ExpandBraces_WithEmptyString_ShouldReturnEmptyString()
    {
        // Arrange
        var pattern = "";

        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().Equal("");
    }

    [Fact]
    public void PrepareTimePatterns_WithSinglePrefix_ShouldOptimizeForIndex()
    {
        // Arrange
        var prefix = "2015-04";
        var regex = "T{13..14}:{00..01}";

        // Act
        var result = _service.PrepareTimePatterns(prefix, regex);

        // Assert
        result.CanOptimizeWithIndex.Should().BeTrue();
        result.SingleRegexPattern.Should().Be("^2015-04");
        result.FieldName.Should().Be("dateString");
    }

    [Fact]
    public void PrepareTimePatterns_WithMultiplePrefixes_ShouldNotOptimize()
    {
        // Arrange
        var prefix = "20{14..15}";
        var regex = "T{13..14}:{00..01}";

        // Act
        var result = _service.PrepareTimePatterns(prefix, regex);

        // Assert
        result.CanOptimizeWithIndex.Should().BeFalse();
        result.SingleRegexPattern.Should().BeNull();
        result.Patterns.Should().NotBeEmpty();
    }

    [Fact]
    public void PrepareTimePatterns_WithCustomFieldName_ShouldUseCustomField()
    {
        // Arrange
        var prefix = "2015";
        string? regex = null;
        var fieldName = "created_at";

        // Act
        var result = _service.PrepareTimePatterns(prefix, regex, fieldName);

        // Assert
        result.FieldName.Should().Be("created_at");
    }

    [Theory]
    [InlineData("T{13..18}:{00..15}", 96)] // 6 hours * 16 minutes = 96 patterns
    [InlineData("T{13..14}:{00..05}", 12)] // 2 hours * 6 minutes = 12 patterns
    [InlineData("T13:00", 1)] // Single pattern
    public void ExpandBraces_WithTimePatterns_ShouldGenerateExpectedCount(
        string pattern,
        int expectedCount
    )
    {
        // Act
        var result = _service.ExpandBraces(pattern);

        // Assert
        result.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void PrepareTimePatterns_LegacyCompatibility_ShouldMatchJavaScriptBehavior()
    {
        // Arrange - Test case from legacy JavaScript implementation
        var prefix = "2015-04";
        var regex = "T{13..18}:{00..15}";

        // Act
        var result = _service.PrepareTimePatterns(prefix, regex);

        // Assert
        result.Patterns.Should().NotBeEmpty();
        result.FieldName.Should().Be("dateString");

        // Should expand the regex part into multiple patterns
        var patterns = result.Patterns.ToList();
        patterns.Should().Contain(p => p.Contains("T13:00"));
        patterns.Should().Contain(p => p.Contains("T18:15"));
    }
}

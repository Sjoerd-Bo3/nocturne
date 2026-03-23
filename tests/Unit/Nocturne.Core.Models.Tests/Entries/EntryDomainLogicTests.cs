using FluentAssertions;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Entries;
using Xunit;

namespace Nocturne.Core.Models.Tests.Entries;

public class EntryDomainLogicTests
{
    // ========================================================================
    // MergeAndDeduplicate
    // ========================================================================

    [Fact]
    public void MergeAndDeduplicate_EmptyInputs_ReturnsEmptyList()
    {
        var result = EntryDomainLogic.MergeAndDeduplicate([], [], count: 10, skip: 0);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MergeAndDeduplicate_OnlyLegacy_ReturnsLegacyOrdered()
    {
        var legacy = new[]
        {
            new Entry { Id = "a", Mills = 1000 },
            new Entry { Id = "b", Mills = 3000 },
            new Entry { Id = "c", Mills = 2000 },
        };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, [], count: 10, skip: 0);

        result.Should().HaveCount(3);
        result[0].Mills.Should().Be(3000);
        result[1].Mills.Should().Be(2000);
        result[2].Mills.Should().Be(1000);
    }

    [Fact]
    public void MergeAndDeduplicate_OnlyProjected_ReturnsProjectedOrdered()
    {
        var projected = new[]
        {
            new Entry { Id = "x", Mills = 5000 },
            new Entry { Id = "y", Mills = 4000 },
        };

        var result = EntryDomainLogic.MergeAndDeduplicate([], projected, count: 10, skip: 0);

        result.Should().HaveCount(2);
        result[0].Mills.Should().Be(5000);
        result[1].Mills.Should().Be(4000);
    }

    [Fact]
    public void MergeAndDeduplicate_DeduplicatesById()
    {
        var legacy = new[] { new Entry { Id = "shared-id", Mills = 1000 } };
        var projected = new[] { new Entry { Id = "shared-id", Mills = 2000 } };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, projected, count: 10, skip: 0);

        result.Should().HaveCount(1);
        result[0].Mills.Should().Be(1000); // Legacy entry kept
    }

    [Fact]
    public void MergeAndDeduplicate_DeduplicatesByMills()
    {
        var legacy = new[] { new Entry { Id = "a", Mills = 1000 } };
        var projected = new[] { new Entry { Id = "b", Mills = 1000 } }; // Same mills, different ID

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, projected, count: 10, skip: 0);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("a"); // Legacy kept
    }

    [Fact]
    public void MergeAndDeduplicate_MergesUniqueEntries()
    {
        var legacy = new[] { new Entry { Id = "a", Mills = 3000 } };
        var projected = new[] { new Entry { Id = "b", Mills = 5000 } };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, projected, count: 10, skip: 0);

        result.Should().HaveCount(2);
        result[0].Mills.Should().Be(5000);
        result[1].Mills.Should().Be(3000);
    }

    [Fact]
    public void MergeAndDeduplicate_AppliesCount()
    {
        var legacy = new[]
        {
            new Entry { Id = "a", Mills = 1000 },
            new Entry { Id = "b", Mills = 2000 },
            new Entry { Id = "c", Mills = 3000 },
        };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, [], count: 2, skip: 0);

        result.Should().HaveCount(2);
        result[0].Mills.Should().Be(3000);
        result[1].Mills.Should().Be(2000);
    }

    [Fact]
    public void MergeAndDeduplicate_AppliesSkip()
    {
        var legacy = new[]
        {
            new Entry { Id = "a", Mills = 1000 },
            new Entry { Id = "b", Mills = 2000 },
            new Entry { Id = "c", Mills = 3000 },
        };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, [], count: 10, skip: 1);

        result.Should().HaveCount(2);
        result[0].Mills.Should().Be(2000);
        result[1].Mills.Should().Be(1000);
    }

    [Fact]
    public void MergeAndDeduplicate_AppliesSkipAndCount()
    {
        var legacy = new[]
        {
            new Entry { Id = "a", Mills = 1000 },
            new Entry { Id = "b", Mills = 2000 },
            new Entry { Id = "c", Mills = 3000 },
            new Entry { Id = "d", Mills = 4000 },
        };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, [], count: 2, skip: 1);

        result.Should().HaveCount(2);
        result[0].Mills.Should().Be(3000);
        result[1].Mills.Should().Be(2000);
    }

    [Fact]
    public void MergeAndDeduplicate_NullIdsInLegacy_DoNotCauseCollisions()
    {
        var legacy = new[] { new Entry { Id = null, Mills = 1000 } };
        var projected = new[] { new Entry { Id = null, Mills = 2000 } };

        var result = EntryDomainLogic.MergeAndDeduplicate(legacy, projected, count: 10, skip: 0);

        // Null IDs should not match — but Mills dedup still applies
        result.Should().HaveCount(2);
    }

    // ========================================================================
    // BuildDemoModeFilterQuery
    // ========================================================================

    [Fact]
    public void BuildDemoModeFilterQuery_DemoEnabled_NullQuery_ReturnsDemoFilter()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: true, existingQuery: null);

        result.Should().Contain("\"data_source\":\"demo-service\"");
        result.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_DemoDisabled_NullQuery_ReturnsExcludeFilter()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: false, existingQuery: null);

        result.Should().Contain("\"data_source\":{\"$ne\":\"demo-service\"}");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_DemoEnabled_EmptyBraces_ReturnsDemoFilter()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: true, existingQuery: "{}");

        result.Should().Contain("\"data_source\":\"demo-service\"");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_MergesWithExistingJsonQuery()
    {
        var existing = "{\"type\":\"sgv\"}";

        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: true, existingQuery: existing);

        result.Should().Contain("\"data_source\":\"demo-service\"");
        result.Should().Contain("\"type\":\"sgv\"");
        result.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_NonJsonQuery_ReturnsDemoFilterOnly()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: true, existingQuery: "not-json");

        result.Should().Contain("\"data_source\":\"demo-service\"");
        result.Should().StartWith("{").And.EndWith("}");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_WhitespaceQuery_ReturnsDemoFilter()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: false, existingQuery: "   ");

        result.Should().Contain("\"data_source\":{\"$ne\":\"demo-service\"}");
    }

    [Fact]
    public void BuildDemoModeFilterQuery_EmptyJsonObject_ReturnsDemoFilter()
    {
        var result = EntryDomainLogic.BuildDemoModeFilterQuery(demoEnabled: true, existingQuery: "{  }");

        result.Should().Contain("\"data_source\":\"demo-service\"");
    }

    // ========================================================================
    // ParseTimeRangeFromFind
    // ========================================================================

    [Fact]
    public void ParseTimeRangeFromFind_NullInput_ReturnsNulls()
    {
        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(null);

        from.Should().BeNull();
        to.Should().BeNull();
    }

    [Fact]
    public void ParseTimeRangeFromFind_EmptyInput_ReturnsNulls()
    {
        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind("");

        from.Should().BeNull();
        to.Should().BeNull();
    }

    [Fact]
    public void ParseTimeRangeFromFind_GteOnly_ReturnsFromOnly()
    {
        var json = """{"date":{"$gte":1700000000000}}""";

        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(json);

        from.Should().Be(1700000000000);
        to.Should().BeNull();
    }

    [Fact]
    public void ParseTimeRangeFromFind_LteOnly_ReturnsToOnly()
    {
        var json = """{"date":{"$lte":1700000000000}}""";

        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(json);

        from.Should().BeNull();
        to.Should().Be(1700000000000);
    }

    [Fact]
    public void ParseTimeRangeFromFind_BothGteAndLte_ReturnsBoth()
    {
        var json = """{"date":{"$gte":1700000000000,"$lte":1700100000000}}""";

        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(json);

        from.Should().Be(1700000000000);
        to.Should().Be(1700100000000);
    }

    [Fact]
    public void ParseTimeRangeFromFind_NoOperators_ReturnsNulls()
    {
        var json = """{"type":"sgv"}""";

        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(json);

        from.Should().BeNull();
        to.Should().BeNull();
    }

    [Fact]
    public void ParseTimeRangeFromFind_MalformedJson_ReturnsNulls()
    {
        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind("not valid json");

        from.Should().BeNull();
        to.Should().BeNull();
    }

    [Fact]
    public void ParseTimeRangeFromFind_NestedInDifferentField_StillParsed()
    {
        var json = """{"mills":{"$gte":100,"$lte":200}}""";

        var (from, to) = EntryDomainLogic.ParseTimeRangeFromFind(json);

        from.Should().Be(100);
        to.Should().Be(200);
    }

    // ========================================================================
    // IsCommonEntryCount
    // ========================================================================

    [Theory]
    [InlineData(10, true)]
    [InlineData(50, true)]
    [InlineData(100, true)]
    [InlineData(1, false)]
    [InlineData(25, false)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(200, false)]
    public void IsCommonEntryCount_ReturnsExpected(int count, bool expected)
    {
        EntryDomainLogic.IsCommonEntryCount(count).Should().Be(expected);
    }

    // ========================================================================
    // SelectMostRecent
    // ========================================================================

    [Fact]
    public void SelectMostRecent_BothNull_ReturnsNull()
    {
        EntryDomainLogic.SelectMostRecent(null, null).Should().BeNull();
    }

    [Fact]
    public void SelectMostRecent_LegacyNull_ReturnsProjected()
    {
        var projected = new Entry { Mills = 1000 };

        EntryDomainLogic.SelectMostRecent(null, projected).Should().BeSameAs(projected);
    }

    [Fact]
    public void SelectMostRecent_ProjectedNull_ReturnsLegacy()
    {
        var legacy = new Entry { Mills = 1000 };

        EntryDomainLogic.SelectMostRecent(legacy, null).Should().BeSameAs(legacy);
    }

    [Fact]
    public void SelectMostRecent_ProjectedHigher_ReturnsProjected()
    {
        var legacy = new Entry { Mills = 1000 };
        var projected = new Entry { Mills = 2000 };

        EntryDomainLogic.SelectMostRecent(legacy, projected).Should().BeSameAs(projected);
    }

    [Fact]
    public void SelectMostRecent_LegacyHigher_ReturnsLegacy()
    {
        var legacy = new Entry { Mills = 3000 };
        var projected = new Entry { Mills = 2000 };

        EntryDomainLogic.SelectMostRecent(legacy, projected).Should().BeSameAs(legacy);
    }

    [Fact]
    public void SelectMostRecent_EqualMills_ReturnsLegacy()
    {
        var legacy = new Entry { Mills = 1000 };
        var projected = new Entry { Mills = 1000 };

        // When equal, projected.Mills > legacy.Mills is false, so legacy is returned
        EntryDomainLogic.SelectMostRecent(legacy, projected).Should().BeSameAs(legacy);
    }

    // ========================================================================
    // ShouldProject
    // ========================================================================

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("sgv", true)]
    [InlineData("cal", false)]
    [InlineData("mbg", false)]
    [InlineData("SGV", false)]  // Case-sensitive
    [InlineData("treatment", false)]
    public void ShouldProject_ReturnsExpected(string? type, bool expected)
    {
        EntryDomainLogic.ShouldProject(type).Should().Be(expected);
    }
}

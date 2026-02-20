using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.API.Services;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Entities.V4;
using Nocturne.Tests.Shared.Infrastructure;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for DataOverviewService.
/// Uses InMemory EF Core via TestDbContextFactory for realistic query execution.
/// </summary>
public class DataOverviewServiceTests : IDisposable
{
    private readonly NocturneDbContext _dbContext;
    private readonly DataOverviewService _service;

    // Well-known timestamps (UTC)
    // 2023-06-15 12:00:00 UTC = 1686830400000
    private const long June15_2023_Noon = 1686830400000L;
    // 2024-01-01 00:00:00 UTC = 1704067200000
    private const long Jan1_2024_Midnight = 1704067200000L;
    // 2024-06-15 12:00:00 UTC = 1718452800000
    private const long June15_2024_Noon = 1718452800000L;
    // 2024-12-31 23:00:00 UTC = 1735686000000
    private const long Dec31_2024_23h = 1735686000000L;
    // 2025-01-01 01:00:00 UTC = 1735693200000
    private const long Jan1_2025_01h = 1735693200000L;

    public DataOverviewServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryContext();
        _service = new DataOverviewService(
            _dbContext,
            NullLogger<DataOverviewService>.Instance
        );
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    #region GetAvailableYearsAsync Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_EmptyDb_ReturnsEmptyYearsAndSources()
    {
        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert
        result.Years.Should().BeEmpty();
        result.AvailableDataSources.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_SingleTableWithData_ReturnsCorrectYear()
    {
        // Arrange - add a single sensor glucose reading in 2024
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert
        result.Years.Should().ContainSingle().Which.Should().Be(2024);
        result.AvailableDataSources.Should().ContainSingle().Which.Should().Be("dexcom");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_MultipleTablesSpanningYears_ReturnsFullRange()
    {
        // Arrange - data spanning 2023 to 2025
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2023_Noon,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = Jan1_2025_01h,
            Insulin = 5.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert - should contain 2023, 2024, 2025
        result.Years.Should().BeEquivalentTo([2023, 2024, 2025]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_DataSourcesCollectedCorrectly()
    {
        // Arrange - multiple data sources across different tables
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1000,
            Insulin = 3.0,
            DataSource = "glooko"
        });
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 2000,
            Carbs = 30.0,
            DataSource = "dexcom" // duplicate source
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon + 3000,
            Source = "medtronic" // StateSpans use Source not DataSource
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert - case-insensitive dedup, ordered alphabetically
        result.AvailableDataSources.Should().HaveCount(3);
        result.AvailableDataSources.Should().ContainInOrder("dexcom", "glooko", "medtronic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_NullDataSourcesNotIncluded()
    {
        // Arrange - records with null DataSource
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = null
        });
        _dbContext.Activities.Add(new ActivityEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert
        result.Years.Should().ContainSingle().Which.Should().Be(2024);
        result.AvailableDataSources.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_StateSpansUseStartMills()
    {
        // Arrange - StateSpan with StartMills in 2023
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2023_Noon,
            EndMills = June15_2023_Noon + 3600000, // 1 hour later
            Source = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert
        result.Years.Should().ContainSingle().Which.Should().Be(2023);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_LegacyEntriesIncluded()
    {
        // Arrange
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2023_Noon,
            Type = "sgv",
            Mgdl = 150.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert
        result.Years.Should().ContainSingle().Which.Should().Be(2023);
        result.AvailableDataSources.Should().ContainSingle().Which.Should().Be("nightscout");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_ActivitiesAndDeviceStatusesIncludedInYears()
    {
        // Arrange - Activities and DeviceStatuses have Mills but no DataSource
        _dbContext.Activities.Add(new ActivityEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2023_Noon
        });
        _dbContext.DeviceStatuses.Add(new DeviceStatusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Device = "test-device"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetAvailableYearsAsync();

        // Assert - years from both tables
        result.Years.Should().BeEquivalentTo([2023, 2024]);
        result.AvailableDataSources.Should().BeEmpty();
    }

    #endregion

    #region GetDailySummaryAsync Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_EmptyDb_ReturnsEmptyDays()
    {
        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Year.Should().Be(2024);
        result.DataSource.Should().BeNull();
        result.Days.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_SingleGlucoseReading_ReturnsCorrectCountAndAverage()
    {
        // Arrange
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 150.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts.Should().ContainKey("Glucose");
        day.Counts["Glucose"].Should().Be(1);
        day.TotalCount.Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(150.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MultipleDataTypesOnSameDay_ReturnsAllCountsAndCorrectTotal()
    {
        // Arrange - multiple data types on the same day (June 15, 2024)
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000, // 5 min later
            Mgdl = 130.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 600000, // 10 min later
            Insulin = 5.0,
            DataSource = "dexcom"
        });
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 900000, // 15 min later
            Carbs = 45.0,
            DataSource = "dexcom"
        });
        _dbContext.Notes.Add(new NoteEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1200000, // 20 min later
            Text = "Feeling good",
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts["Glucose"].Should().Be(2);
        day.Counts["Boluses"].Should().Be(1);
        day.Counts["CarbIntake"].Should().Be(1);
        day.Counts["Notes"].Should().Be(1);
        day.TotalCount.Should().Be(5);
        // Average of 120 and 130
        day.AverageGlucoseMgdl.Should().Be(125.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataSourceFilter_OnlyMatchingRecords()
    {
        // Arrange - data from two sources on the same day
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Mgdl = 200.0,
            DataSource = "glooko"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 600000,
            Insulin = 3.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 900000,
            Insulin = 7.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act - filter to dexcom only
        var result = await _service.GetDailySummaryAsync(2024, "dexcom");

        // Assert
        result.DataSource.Should().Be("dexcom");
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.Counts["Boluses"].Should().Be(1);
        day.TotalCount.Should().Be(2);
        // Average should be 120 only (the dexcom reading)
        day.AverageGlucoseMgdl.Should().Be(120.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_StateSpansUseStartMillsAndSource()
    {
        // Arrange - StateSpans use StartMills and Source (not Mills/DataSource)
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon,
            Source = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act - no filter
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts.Should().ContainKey("StateSpans");
        day.Counts["StateSpans"].Should().Be(1);
        day.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_StateSpansFilteredBySource()
    {
        // Arrange
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon,
            Source = "glooko"
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Manual",
            StartMills = June15_2024_Noon + 300000,
            Source = "medtronic"
        });
        await _dbContext.SaveChangesAsync();

        // Act - filter by glooko
        var result = await _service.GetDailySummaryAsync(2024, "glooko");

        // Assert - should only include glooko StateSpan
        result.Days.Should().ContainSingle();
        result.Days[0].Counts["StateSpans"].Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_ActivitiesExcludedWhenDataSourceFilterActive()
    {
        // Arrange - Activities have no DataSource column
        _dbContext.Activities.Add(new ActivityEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1000,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act - with dataSource filter, Activities should be excluded
        var result = await _service.GetDailySummaryAsync(2024, "dexcom");

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().NotContainKey("Activity");
        day.Counts.Should().ContainKey("Glucose");
        day.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DeviceStatusesExcludedWhenDataSourceFilterActive()
    {
        // Arrange - DeviceStatuses have no DataSource column
        _dbContext.DeviceStatuses.Add(new DeviceStatusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Device = "test-device"
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1000,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act - with dataSource filter, DeviceStatuses should be excluded
        var result = await _service.GetDailySummaryAsync(2024, "dexcom");

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().NotContainKey("DeviceStatus");
        day.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_ActivitiesAndDeviceStatusesIncludedWithoutFilter()
    {
        // Arrange
        _dbContext.Activities.Add(new ActivityEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon
        });
        _dbContext.DeviceStatuses.Add(new DeviceStatusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1000,
            Device = "test-device"
        });
        await _dbContext.SaveChangesAsync();

        // Act - no filter
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().ContainKey("Activity");
        day.Counts.Should().ContainKey("DeviceStatus");
        day.Counts["Activity"].Should().Be(1);
        day.Counts["DeviceStatus"].Should().Be(1);
        day.TotalCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntrySgv_CountedAsGlucose()
    {
        // Arrange - legacy entry with type="sgv"
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Type = "sgv",
            Mgdl = 140.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().ContainKey("Glucose");
        day.Counts["Glucose"].Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(140.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntryMbg_CountedAsManualBG()
    {
        // Arrange - legacy entry with type="mbg"
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Type = "mbg",
            Mgdl = 160.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().ContainKey("ManualBG");
        day.Counts["ManualBG"].Should().Be(1);
        // mbg entries do NOT contribute to glucose average (only sgv does)
        day.AverageGlucoseMgdl.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MixedSensorAndLegacySgv_CombinedGlucoseAverage()
    {
        // Arrange - both SensorGlucose and legacy sgv entries
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Type = "sgv",
            Mgdl = 200.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        var day = result.Days[0];
        // Glucose count: 1 from SensorGlucose + 1 from legacy sgv = 2
        day.Counts["Glucose"].Should().Be(2);
        // Average of 100 and 200 = 150.0
        day.AverageGlucoseMgdl.Should().Be(150.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataOutsideYearExcluded()
    {
        // Arrange - data in 2023 and 2024, query for 2024 only
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2023_Noon,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 200.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert - only the 2024 data
        result.Days.Should().ContainSingle();
        result.Days[0].Date.Should().Be("2024-06-15");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataSourceFilterWithNoMatches_ReturnsEmptyDays()
    {
        // Arrange - data with one source
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act - filter by nonexistent source
        var result = await _service.GetDailySummaryAsync(2024, "nonexistent-source");

        // Assert
        result.Days.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_YearBoundary_Dec31ToJan1()
    {
        // Arrange - data on Dec 31 2024 and Jan 1 2025
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = Dec31_2024_23h,
            Mgdl = 110.0,
            DataSource = "dexcom"
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = Jan1_2025_01h,
            Mgdl = 130.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act - query 2024
        var result2024 = await _service.GetDailySummaryAsync(2024);
        // Act - query 2025
        var result2025 = await _service.GetDailySummaryAsync(2025);

        // Assert
        result2024.Days.Should().ContainSingle();
        result2024.Days[0].Date.Should().Be("2024-12-31");
        result2024.Days[0].AverageGlucoseMgdl.Should().Be(110.0);

        result2025.Days.Should().ContainSingle();
        result2025.Days[0].Date.Should().Be("2025-01-01");
        result2025.Days[0].AverageGlucoseMgdl.Should().Be(130.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MultipleDays_OrderedByDate()
    {
        // Arrange - data on multiple days, added out of order
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon, // June 15
            Mgdl = 100.0
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = Jan1_2024_Midnight, // Jan 1
            Mgdl = 200.0
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert - should be ordered by date
        result.Days.Should().HaveCount(2);
        result.Days[0].Date.Should().Be("2024-01-01");
        result.Days[1].Date.Should().Be("2024-06-15");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_GlucoseAverageRoundedToOneDecimal()
    {
        // Arrange - values that produce a repeating decimal average
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 100.0
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Mgdl = 133.0
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 600000,
            Mgdl = 150.0
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        // Average of (100 + 133 + 150) / 3 = 127.666... -> rounded to 127.7
        result.Days[0].AverageGlucoseMgdl.Should().Be(127.7);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MeterGlucoseCountedAsManualBG()
    {
        // Arrange
        _dbContext.MeterGlucose.Add(new MeterGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 130.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().ContainKey("ManualBG");
        day.Counts["ManualBG"].Should().Be(1);
        day.TotalCount.Should().Be(1);
        // MeterGlucose does NOT contribute to the glucose average
        day.AverageGlucoseMgdl.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BolusCalculationsAndDeviceEvents_CountedCorrectly()
    {
        // Arrange
        _dbContext.BolusCalculations.Add(new BolusCalculationEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            DataSource = "glooko"
        });
        _dbContext.DeviceEvents.Add(new DeviceEventEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1000,
            EventType = "SiteChange",
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().ContainKey("BolusCalculations");
        day.Counts["BolusCalculations"].Should().Be(1);
        day.Counts.Should().ContainKey("DeviceEvents");
        day.Counts["DeviceEvents"].Should().Be(1);
        day.TotalCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntriesFilteredByDataSource()
    {
        // Arrange
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Type = "sgv",
            Mgdl = 120.0,
            DataSource = "nightscout"
        });
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Type = "sgv",
            Mgdl = 180.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        // Act - filter by nightscout
        var result = await _service.GetDailySummaryAsync(2024, "nightscout");

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(120.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_NoGlucoseData_AverageIsNull()
    {
        // Arrange - only non-glucose data
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetDailySummaryAsync(2024);

        // Assert
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.AverageGlucoseMgdl.Should().BeNull();
        day.Counts["Boluses"].Should().Be(1);
    }

    #endregion
}

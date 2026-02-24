using System.Text.Json;
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
        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().BeEmpty();
        result.AvailableDataSources.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_SingleTableWithData_ReturnsCorrectYear()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().ContainSingle().Which.Should().Be(2024);
        result.AvailableDataSources.Should().ContainSingle().Which.Should().Be("dexcom");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_MultipleTablesSpanningYears_ReturnsFullRange()
    {
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

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().BeEquivalentTo([2023, 2024, 2025]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_DataSourcesCollectedCorrectly()
    {
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
            DataSource = "dexcom"
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon + 3000,
            Source = "medtronic"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAvailableYearsAsync();

        result.AvailableDataSources.Should().HaveCount(3);
        result.AvailableDataSources.Should().ContainInOrder("dexcom", "glooko", "medtronic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_NullDataSourcesNotIncluded()
    {
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

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().ContainSingle().Which.Should().Be(2024);
        result.AvailableDataSources.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_StateSpansUseStartMills()
    {
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2023_Noon,
            EndMills = June15_2023_Noon + 3600000,
            Source = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().ContainSingle().Which.Should().Be(2023);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_LegacyEntriesIncluded()
    {
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2023_Noon,
            Type = "sgv",
            Mgdl = 150.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().ContainSingle().Which.Should().Be(2023);
        result.AvailableDataSources.Should().ContainSingle().Which.Should().Be("nightscout");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetAvailableYearsAsync_ActivitiesAndDeviceStatusesIncludedInYears()
    {
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

        var result = await _service.GetAvailableYearsAsync();

        result.Years.Should().BeEquivalentTo([2023, 2024]);
        result.AvailableDataSources.Should().BeEmpty();
    }

    #endregion

    #region GetDailySummaryAsync Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_EmptyDb_ReturnsEmptyDays()
    {
        var result = await _service.GetDailySummaryAsync(2024);

        result.Year.Should().Be(2024);
        result.DataSources.Should().BeNull();
        result.Days.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_SingleGlucoseReading_ReturnsCorrectCountAndAverage()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 150.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts["Glucose"].Should().Be(1);
        day.TotalCount.Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(150.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MultipleDataTypesOnSameDay_ReturnsAllCountsAndCorrectTotal()
    {
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
            Mgdl = 130.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 600000,
            Insulin = 5.0,
            DataSource = "dexcom"
        });
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 900000,
            Carbs = 45.0,
            DataSource = "dexcom"
        });
        _dbContext.Notes.Add(new NoteEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 1200000,
            Text = "Feeling good",
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts["Glucose"].Should().Be(2);
        day.Counts["Boluses"].Should().Be(1);
        day.Counts["CarbIntake"].Should().Be(1);
        day.Counts["Notes"].Should().Be(1);
        day.TotalCount.Should().Be(5);
        day.AverageGlucoseMgdl.Should().Be(125.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataSourceFilter_OnlyMatchingRecords()
    {
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

        var result = await _service.GetDailySummaryAsync(2024, ["dexcom"]);

        result.DataSources.Should().BeEquivalentTo(["dexcom"]);
        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.Counts["Boluses"].Should().Be(1);
        day.TotalCount.Should().Be(2);
        day.AverageGlucoseMgdl.Should().Be(120.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MultipleDataSourceFilter_MatchesAll()
    {
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
            DataSource = "medtronic"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024, ["dexcom", "glooko"]);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(2);
        day.Counts.Should().NotContainKey("Boluses");
        day.AverageGlucoseMgdl.Should().Be(160.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_StateSpansUseStartMillsAndSource()
    {
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon,
            Source = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Date.Should().Be("2024-06-15");
        day.Counts["StateSpans"].Should().Be(1);
        day.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_StateSpansFilteredBySource()
    {
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

        var result = await _service.GetDailySummaryAsync(2024, ["glooko"]);

        result.Days.Should().ContainSingle();
        result.Days[0].Counts["StateSpans"].Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_ActivitiesExcludedWhenDataSourceFilterActive()
    {
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

        var result = await _service.GetDailySummaryAsync(2024, ["dexcom"]);

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

        var result = await _service.GetDailySummaryAsync(2024, ["dexcom"]);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts.Should().NotContainKey("DeviceStatus");
        day.TotalCount.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_ActivitiesAndDeviceStatusesIncludedWithoutFilter()
    {
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

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Activity"].Should().Be(1);
        day.Counts["DeviceStatus"].Should().Be(1);
        day.TotalCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntrySgv_CountedAsGlucose()
    {
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Type = "sgv",
            Mgdl = 140.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(140.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntryMbg_CountedAsManualBGAndContributesToAverage()
    {
        _dbContext.Entries.Add(new EntryEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Type = "mbg",
            Mgdl = 160.0,
            DataSource = "nightscout"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["ManualBG"].Should().Be(1);
        // mbg entries contribute to glucose average
        day.AverageGlucoseMgdl.Should().Be(160.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MixedSensorAndLegacySgv_CombinedGlucoseAverage()
    {
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

        var result = await _service.GetDailySummaryAsync(2024);

        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(2);
        day.AverageGlucoseMgdl.Should().Be(150.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataOutsideYearExcluded()
    {
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

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        result.Days[0].Date.Should().Be("2024-06-15");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_DataSourceFilterWithNoMatches_ReturnsEmptyDays()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024, ["nonexistent-source"]);

        result.Days.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_YearBoundary_Dec31ToJan1()
    {
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

        var result2024 = await _service.GetDailySummaryAsync(2024);
        var result2025 = await _service.GetDailySummaryAsync(2025);

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
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 100.0
        });
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = Jan1_2024_Midnight,
            Mgdl = 200.0
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().HaveCount(2);
        result.Days[0].Date.Should().Be("2024-01-01");
        result.Days[1].Date.Should().Be("2024-06-15");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_GlucoseAverageRoundedToOneDecimal()
    {
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

        var result = await _service.GetDailySummaryAsync(2024);

        // Average of (100 + 133 + 150) / 3 = 127.666... -> rounded to 127.7
        result.Days[0].AverageGlucoseMgdl.Should().Be(127.7);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MeterGlucoseContributesToGlucoseAverage()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 100.0,
            DataSource = "dexcom"
        });
        _dbContext.MeterGlucose.Add(new MeterGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Mgdl = 200.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.Counts["ManualBG"].Should().Be(1);
        // Average includes both sensor and meter: (100 + 200) / 2 = 150
        day.AverageGlucoseMgdl.Should().Be(150.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BolusCalculationsAndDeviceEvents_CountedCorrectly()
    {
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

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["BolusCalculations"].Should().Be(1);
        day.Counts["DeviceEvents"].Should().Be(1);
        day.TotalCount.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_LegacyEntriesFilteredByDataSource()
    {
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

        var result = await _service.GetDailySummaryAsync(2024, ["nightscout"]);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.Counts["Glucose"].Should().Be(1);
        day.AverageGlucoseMgdl.Should().Be(120.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_NoGlucoseData_AverageIsNull()
    {
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.AverageGlucoseMgdl.Should().BeNull();
        day.Counts["Boluses"].Should().Be(1);
    }

    #endregion

    #region Insulin Totals Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BolusInsulin_CalculatedCorrectly()
    {
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.5,
            DataSource = "glooko"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Insulin = 3.2,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBolusUnits.Should().Be(8.7);
        day.TotalBasalUnits.Should().BeNull();
        day.TotalDailyDose.Should().Be(8.7);
    }

    // TODO: BasalInsulin test will be rewritten in Task 14 to use MicroBolus records
    // The old test used IsBasalInsulin flag on Bolus which has been removed

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_MultipleBoluses_AllCountAsBolus()
    {
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.0,
            DataSource = "glooko"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Insulin = 10.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBolusUnits.Should().Be(15.0);
        day.TotalDailyDose.Should().Be(15.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_NoInsulinData_InsulinFieldsNull()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120.0,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBolusUnits.Should().BeNull();
        day.TotalBasalUnits.Should().BeNull();
        day.TotalDailyDose.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_InsulinFilteredByDataSource()
    {
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.0,
            DataSource = "dexcom"
        });
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Insulin = 10.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024, ["dexcom"]);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBolusUnits.Should().Be(5.0);
        day.TotalDailyDose.Should().Be(5.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BasalFromStateSpans_CalculatedFromMetadata()
    {
        // Two basal delivery spans: 1 U/hr for 1800s (0.5U) + 0.8 U/hr for 3600s (0.8U) = 1.3U total
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "BasalDelivery",
            State = "Active",
            StartMills = June15_2024_Noon,
            EndMills = June15_2024_Noon + 1800000,
            Source = "glooko",
            MetadataJson = JsonSerializer.Serialize(new { rate = 1.0, origin = "Scheduled", durationSeconds = 1800, calculatedInsulin = 0.5 })
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "BasalDelivery",
            State = "Active",
            StartMills = June15_2024_Noon + 1800000,
            EndMills = June15_2024_Noon + 5400000,
            Source = "glooko",
            MetadataJson = JsonSerializer.Serialize(new { rate = 0.8, origin = "Algorithm", durationSeconds = 3600, calculatedInsulin = 0.8 })
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBasalUnits.Should().Be(1.3);
        day.TotalDailyDose.Should().Be(1.3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BasalFromStateSpans_CombinedWithBolusForTdd()
    {
        // Bolus: 5U, Basal from StateSpan: 10U -> TDD = 15U
        _dbContext.Boluses.Add(new BolusEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Insulin = 5.0,
            DataSource = "glooko"
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "BasalDelivery",
            State = "Active",
            StartMills = June15_2024_Noon,
            EndMills = June15_2024_Noon + 36000000,
            Source = "glooko",
            MetadataJson = JsonSerializer.Serialize(new { rate = 1.0, origin = "Scheduled", durationSeconds = 36000, calculatedInsulin = 10.0 })
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBolusUnits.Should().Be(5.0);
        day.TotalBasalUnits.Should().Be(10.0);
        day.TotalDailyDose.Should().Be(15.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_BasalStateSpans_FilteredBySource()
    {
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "BasalDelivery",
            State = "Active",
            StartMills = June15_2024_Noon,
            Source = "glooko",
            MetadataJson = JsonSerializer.Serialize(new { rate = 1.0, calculatedInsulin = 5.0 })
        });
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "BasalDelivery",
            State = "Active",
            StartMills = June15_2024_Noon + 300000,
            Source = "medtronic",
            MetadataJson = JsonSerializer.Serialize(new { rate = 0.8, calculatedInsulin = 3.0 })
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024, ["glooko"]);

        result.Days.Should().ContainSingle();
        result.Days[0].TotalBasalUnits.Should().Be(5.0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_NonBasalStateSpans_NotCountedAsInsulin()
    {
        // PumpMode StateSpan should NOT contribute to basal insulin
        _dbContext.StateSpans.Add(new StateSpanEntity
        {
            Id = Guid.NewGuid(),
            Category = "PumpMode",
            State = "Automatic",
            StartMills = June15_2024_Noon,
            Source = "glooko",
            MetadataJson = JsonSerializer.Serialize(new { mode = "Auto" })
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalBasalUnits.Should().BeNull();
        day.TotalDailyDose.Should().BeNull();
    }

    #endregion

    #region Carb Totals Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_CarbTotals_CalculatedCorrectly()
    {
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Carbs = 45.0,
            DataSource = "mylife"
        });
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 3600000, // 1 hour later, same day
            Carbs = 30.5,
            DataSource = "mylife"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        var day = result.Days[0];
        day.TotalCarbs.Should().Be(75.5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_NoCarbData_CarbFieldNull()
    {
        _dbContext.SensorGlucose.Add(new SensorGlucoseEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Mgdl = 120,
            DataSource = "dexcom"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024);

        result.Days.Should().ContainSingle();
        result.Days[0].TotalCarbs.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetDailySummaryAsync_CarbsFilteredByDataSource()
    {
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon,
            Carbs = 50.0,
            DataSource = "mylife"
        });
        _dbContext.CarbIntakes.Add(new CarbIntakeEntity
        {
            Id = Guid.NewGuid(),
            Mills = June15_2024_Noon + 300000,
            Carbs = 25.0,
            DataSource = "glooko"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetDailySummaryAsync(2024, ["mylife"]);

        result.Days.Should().ContainSingle();
        result.Days[0].TotalCarbs.Should().Be(50.0);
    }

    #endregion
}

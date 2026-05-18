using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services.Analytics;
using Nocturne.Core.Contracts.Analytics;
using Nocturne.Core.Contracts.Legacy;
using Nocturne.Core.Contracts.Profiles;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.Analytics;

/// <summary>
/// Unit tests for SummaryService
/// Tests the 1:1 compatibility with legacy JavaScript implementation
/// </summary>
public class SummaryServiceTests
{
    private readonly Mock<IDDataService> _mockDDataService;
    private readonly Mock<IPropertiesService> _mockPropertiesService;
    private readonly Mock<ILogger<SummaryService>> _mockLogger;
    private readonly SummaryService _service;

    public SummaryServiceTests()
    {
        _mockDDataService = new Mock<IDDataService>();
        _mockPropertiesService = new Mock<IPropertiesService>();
        _mockLogger = new Mock<ILogger<SummaryService>>();
        _service = new SummaryService(
            _mockDDataService.Object,
            _mockPropertiesService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task GetSummaryAsync_WithValidData_ReturnsProcessedSummary()
    {
        // Arrange
        var testDData = CreateTestDData();
        var testProperties = CreateTestProperties();

        _mockDDataService
            .Setup(x => x.GetDDataAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(testDData);

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(testProperties);

        // Act
        var result = await _service.GetSummaryAsync(6, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Sgvs.Should().HaveCount(2); // Should filter entries within time window
        result.Treatments.Should().NotBeNull();
        result.Profile.Should().NotBeNull();
        result.State.Should().NotBeNull();
    }

    [Fact]
    public void ProcessSgvs_WithValidEntries_FiltersAndFormatsCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var sgvs = new List<Entry>
        {
            new Entry
            {
                Mills = currentTime - 1000,
                Mgdl = 120,
                Noise = 1,
            },
            new Entry
            {
                Mills = currentTime - 2 * 60 * 60 * 1000,
                Mgdl = 110,
                Noise = 2,
            }, // 2 hours ago
            new Entry { Mills = currentTime - 8 * 60 * 60 * 1000, Mgdl = 100 }, // 8 hours ago, should be filtered out
        };

        // Act
        var result = _service.ProcessSgvs(sgvs, 6);

        // Assert
        result.Should().HaveCount(2);
        result[0].Sgv.Should().Be(120);
        result[0].Noise.Should().BeNull(); // Noise of 1 should be excluded
        result[1].Sgv.Should().Be(110);
        result[1].Noise.Should().Be(2); // Noise != 1 should be included
    }

    [Fact]
    public void ProcessTreatments_WithInsulinAndCarbs_ExtractsCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatments = new List<Treatment>
        {
            new Treatment
            {
                Mills = currentTime - 1000,
                EventType = "Meal Bolus",
                Insulin = 5.5,
                Carbs = 30,
            },
            new Treatment
            {
                Mills = currentTime - 2000,
                EventType = "Correction Bolus",
                Insulin = 2.0,
            },
            new Treatment
            {
                Mills = currentTime - 8 * 60 * 60 * 1000, // 8 hours ago
                EventType = "Meal Bolus",
                Insulin = 3.0,
            },
        };

        // Act
        var result = _service.ProcessTreatments(treatments, null, 6);

        // Assert
        result.Treatments.Should().HaveCount(2); // Should filter out 8-hour old treatment
        result.Treatments[0].Insulin.Should().Be(5.5);
        result.Treatments[0].Carbs.Should().Be(30);
        result.Treatments[1].Insulin.Should().Be(2.0);
        result.Treatments[1].Carbs.Should().BeNull();
    }

    [Fact]
    public void ProcessTreatments_WithTempBasals_ProcessesCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatments = new List<Treatment>
        {
            new Treatment
            {
                Mills = currentTime - 1000,
                EventType = "Temp Basal",
                Duration = 60, // 60 minutes
                Absolute = 1.5,
                Created_at = DateTimeOffset
                    .FromUnixTimeMilliseconds(currentTime - 1000)
                    .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            },
        };
        var profileData = new ProfileData
        {
            Basal = new List<TimeValue>
            {
                new TimeValue { Time = "00:00", Value = 1.0 },
                new TimeValue { Time = "06:00", Value = 1.2 },
            },
        };

        // Act
        var result = _service.ProcessTreatments(treatments, profileData, 6);

        // Assert
        result.TempBasals.Should().NotBeEmpty();
    }

    [Fact]
    public void ProcessTreatments_WithTemporaryTargets_ExtractsCorrectly()
    {
        // Arrange
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var treatments = new List<Treatment>
        {
            new Treatment
            {
                Mills = currentTime - 1000,
                EventType = "Temporary Target",
                TargetTop = 180.5,
                TargetBottom = 100.3,
                Duration = 120, // 120 minutes
            },
        };

        // Act
        var result = _service.ProcessTreatments(treatments, null, 6);

        // Assert
        result.Targets.Should().HaveCount(1);
        result.Targets[0].TargetTop.Should().Be(181); // Should be rounded
        result.Targets[0].TargetBottom.Should().Be(100); // Should be rounded
        result.Targets[0].Duration.Should().Be(7200); // Should be converted to seconds (120 * 60)
    }

    [Fact]
    public async Task ConstructStateAsync_WithValidProperties_ReturnsCorrectState()
    {
        // Arrange
        var properties = CreateTestProperties();
        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Iob.Should().Be(2.5);
        result.Cob.Should().Be(25);
        result.Bwp.Should().Be(1.23);
        result.Cage.Should().Be(48);
        result.Sage.Should().Be(72);
        result.Battery.Should().Be(85);
    }

    [Fact]
    public void FilterSameAbsoluteTemps_WithIdenticalRates_MergesCorrectly()
    {
        // Arrange
        var tempBasals = new List<SummaryTempBasal>
        {
            new SummaryTempBasal
            {
                Start = 1000,
                Duration = 3600,
                Absolute = 1.5,
            }, // 1 hour
            new SummaryTempBasal
            {
                Start = 4600,
                Duration = 1800,
                Absolute = 1.5,
            }, // 30 minutes, same rate
            new SummaryTempBasal
            {
                Start = 6400,
                Duration = 1800,
                Absolute = 2.0,
            }, // 30 minutes, different rate
        };

        // Act
        var result = _service.FilterSameAbsoluteTemps(tempBasals);

        // Assert
        result.Should().HaveCount(2); // First two should be merged
        result[0].Duration.Should().Be(5400); // 3600 + 1800 = 5400
        result[0].Absolute.Should().Be(1.5);
        result[1].Absolute.Should().Be(2.0);
    }

    [Fact]
    public void GetProfileBasalsInWindow_WithValidProfile_ReturnsCorrectBasals()
    {
        // Arrange
        var basals = new List<TimeValue>
        {
            new TimeValue { Time = "00:00", Value = 1.0 },
            new TimeValue { Time = "06:00", Value = 1.2 },
            new TimeValue { Time = "12:00", Value = 1.5 },
            new TimeValue { Time = "18:00", Value = 1.3 },
        };

        var start = DateTimeOffset.Parse("2023-01-01T08:00:00Z").ToUnixTimeMilliseconds();
        var end = DateTimeOffset.Parse("2023-01-01T14:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var result = _service.GetProfileBasalsInWindow(basals, start, end);

        // Assert
        result.Should().NotBeEmpty();
        result.All(b => b.Profile == 1).Should().BeTrue(); // All should be marked as profile basals
        result.Should().Contain(b => b.Absolute == 1.2); // Should include 06:00 rate
        result.Should().Contain(b => b.Absolute == 1.5); // Should include 12:00 rate
    }

    private DData CreateTestDData()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new DData
        {
            Sgvs = new List<Entry>
            {
                new Entry
                {
                    Mills = currentTime - 1000,
                    Mgdl = 120,
                    Noise = 1,
                },
                new Entry
                {
                    Mills = currentTime - 2 * 60 * 60 * 1000,
                    Mgdl = 110,
                    Noise = 2,
                },
                new Entry { Mills = currentTime - 8 * 60 * 60 * 1000, Mgdl = 100 },
            },
            Treatments = new List<Treatment>
            {
                new Treatment
                {
                    Mills = currentTime - 1000,
                    EventType = "Meal Bolus",
                    Insulin = 5.5,
                    Carbs = 30,
                },
            },
            Profiles = new List<Profile>
            {
                new Profile
                {
                    Mills = currentTime,
                    DefaultProfile = "Default",
                    Store = new Dictionary<string, ProfileData>
                    {
                        ["Default"] = new ProfileData
                        {
                            Units = "mg/dl",
                            Basal = new List<TimeValue>
                            {
                                new TimeValue { Time = "00:00", Value = 1.0 },
                            },
                        },
                    },
                },
            },
        };
    }

    private Dictionary<string, object> CreateTestProperties()
    {
        return new Dictionary<string, object>
        {
            ["iob"] = new Dictionary<string, object> { ["iob"] = 2.5 },
            ["cob"] = new Dictionary<string, object> { ["cob"] = 25 },
            ["bwp"] = new Dictionary<string, object> { ["bolusEstimate"] = 1.234 },
            ["cage"] = new Dictionary<string, object> { ["age"] = 48 },
            ["sage"] = new Dictionary<string, object> { ["age"] = 72 },
            ["iage"] = new Dictionary<string, object> { ["age"] = 96 },
            ["bage"] = new Dictionary<string, object> { ["age"] = 24 },
            ["upbat"] = new Dictionary<string, object> { ["level"] = 85 },
        };
    }

    [Fact]
    public async Task ConstructStateAsync_WithCalibrationAge_IncludesCalibrationAge()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["calib"] = new Dictionary<string, object> { ["age"] = 12 }, // 12 hours since last calibration
        };

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.CalibAge.Should().Be(12);
    }

    [Fact]
    public async Task ConstructStateAsync_WithoutCalibrationAge_CalibrationAgeIsNull()
    {
        // Arrange
        var properties = new Dictionary<string, object>();

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.CalibAge.Should().BeNull();
    }

    [Fact]
    public async Task ConstructStateAsync_WithSensorAge_CalculatesSensorExpiration()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["sage"] = new Dictionary<string, object> { ["age"] = 120 }, // 120 hours (5 days) since sensor start
        };

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.Sage.Should().Be(120);
        result.SensorExpiresIn.Should().Be(120); // 240 - 120 = 120 hours remaining
    }

    [Fact]
    public async Task ConstructStateAsync_WithOldSensorAge_SensorExpiresInIsZero()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["sage"] = new Dictionary<string, object> { ["age"] = 300 }, // 300 hours (12.5 days) - expired
        };

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.Sage.Should().Be(300);
        result.SensorExpiresIn.Should().Be(0); // Sensor already expired
    }

    [Fact]
    public async Task ConstructStateAsync_WithoutSensorAge_SensorExpiresInIsNull()
    {
        // Arrange
        var properties = new Dictionary<string, object>();

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.Sage.Should().BeNull();
        result.SensorExpiresIn.Should().BeNull();
    }

    [Fact]
    public async Task ConstructStateAsync_WithAllNewProperties_IncludesAllValues()
    {
        // Arrange
        var properties = new Dictionary<string, object>
        {
            ["cage"] = new Dictionary<string, object> { ["age"] = 48 },
            ["sage"] = new Dictionary<string, object> { ["age"] = 72 },
            ["iage"] = new Dictionary<string, object> { ["age"] = 96 },
            ["bage"] = new Dictionary<string, object> { ["age"] = 24 },
            ["calib"] = new Dictionary<string, object> { ["age"] = 6 },
            ["iob"] = new Dictionary<string, object> { ["iob"] = 2.5 },
            ["cob"] = new Dictionary<string, object> { ["cob"] = 25 },
            ["upbat"] = new Dictionary<string, object> { ["level"] = 85 },
        };

        _mockPropertiesService
            .Setup(x => x.GetAllPropertiesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(properties);

        // Act
        var result = await _service.ConstructStateAsync(CancellationToken.None);

        // Assert
        result.Cage.Should().Be(48);
        result.Sage.Should().Be(72);
        result.Iage.Should().Be(96);
        result.Bage.Should().Be(24);
        result.CalibAge.Should().Be(6);
        result.SensorExpiresIn.Should().Be(168); // 240 - 72 = 168 hours remaining
        result.Iob.Should().Be(2.5);
        result.Cob.Should().Be(25);
        result.Battery.Should().Be(85);
    }

    private Dictionary<string, object> CreateTestPropertiesWithCalibration()
    {
        return new Dictionary<string, object>
        {
            ["iob"] = new Dictionary<string, object> { ["iob"] = 2.5 },
            ["cob"] = new Dictionary<string, object> { ["cob"] = 25 },
            ["bwp"] = new Dictionary<string, object> { ["bolusEstimate"] = 1.234 },
            ["cage"] = new Dictionary<string, object> { ["age"] = 48 },
            ["sage"] = new Dictionary<string, object> { ["age"] = 72 },
            ["iage"] = new Dictionary<string, object> { ["age"] = 96 },
            ["bage"] = new Dictionary<string, object> { ["age"] = 24 },
            ["calib"] = new Dictionary<string, object> { ["age"] = 12 },
            ["upbat"] = new Dictionary<string, object> { ["level"] = 85 },
        };
    }
}

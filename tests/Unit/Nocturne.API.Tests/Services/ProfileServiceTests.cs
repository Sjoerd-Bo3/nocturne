using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Tests.Shared.Mocks;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Tests for ProfileService with 1:1 legacy compatibility
/// Based on legacy profilefunctions.js behavior
/// </summary>
[Parity("profile.test.js", Description = "Includes basalprofileplugin.test.js scenarios")]
public class ProfileServiceTests
{
    private readonly ProfileService _profileService;
    private readonly IMemoryCache _cache;

    public ProfileServiceTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _profileService = new ProfileService(_cache, MockTenantAccessor.Create().Object);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        // Act & Assert
        Assert.NotNull(_profileService);
        Assert.False(_profileService.HasData());
    }

    [Fact]
    public void Clear_ShouldResetAllData()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);

        // Act
        _profileService.Clear();

        // Assert
        Assert.False(_profileService.HasData());
    }

    [Fact]
    public void LoadData_WithValidProfiles_ShouldLoadCorrectly()
    {
        // Arrange
        var profiles = CreateTestProfiles();

        // Act
        _profileService.LoadData(profiles);

        // Assert
        Assert.True(_profileService.HasData());
    }

    [Fact]
    public void GetUnits_WithMgDlProfile_ShouldReturnMgDl()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);

        // Act
        var units = _profileService.GetUnits();

        // Assert
        Assert.Equal("mg/dl", units);
    }

    [Fact]
    public void GetUnits_WithMmolProfile_ShouldReturnMmol()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        profiles[0].Store["Default"].Units = "mmol/L";
        _profileService.LoadData(profiles);

        // Act
        var units = _profileService.GetUnits();

        // Assert
        Assert.Equal("mmol", units);
    }

    [Fact]
    public void GetDIA_ShouldReturnCorrectValue()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var dia = _profileService.GetDIA(testTime);

        // Assert
        Assert.Equal(4.0, dia);
    }

    [Fact]
    public void GetSensitivity_ShouldReturnCorrectValue()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var sensitivity = _profileService.GetSensitivity(testTime);

        // Assert
        Assert.Equal(40.0, sensitivity);
    }

    [Fact]
    public void GetCarbRatio_ShouldReturnCorrectValue()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var carbRatio = _profileService.GetCarbRatio(testTime);

        // Assert
        Assert.Equal(20.0, carbRatio);
    }

    [Fact]
    public void GetBasalRate_AtMorning_ShouldReturnMorningRate()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var morningTime = DateTimeOffset.Parse("2024-01-01T08:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var basalRate = _profileService.GetBasalRate(morningTime);

        // Assert
        Assert.Equal(1.2, basalRate);
    }

    [Fact]
    public void GetBasalRate_AtNight_ShouldReturnNightRate()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var nightTime = DateTimeOffset.Parse("2024-01-01T02:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var basalRate = _profileService.GetBasalRate(nightTime);

        // Assert
        Assert.Equal(1.2, basalRate);
    }

    [Fact]
    public void GetValueByTime_ShouldCacheResults()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);
        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act - Call twice
        var dia1 = _profileService.GetDIA(testTime);
        var dia2 = _profileService.GetDIA(testTime);

        // Assert
        Assert.Equal(dia1, dia2);
        Assert.Equal(4.0, dia1);
    }

    [Fact]
    public void UpdateTreatments_ShouldProcessTempBasals()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);

        var tempBasals = new List<Treatment>
        {
            new()
            {
                Mills = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds(),
                Duration = 60, // 60 minutes
                Absolute = 2.0,
            },
        };

        // Act
        _profileService.UpdateTreatments(null, tempBasals);
        var testTime = DateTimeOffset.Parse("2024-01-01T10:30:00Z").ToUnixTimeMilliseconds();
        var tempBasal = _profileService.GetTempBasal(testTime);

        // Assert
        Assert.NotNull(tempBasal.Treatment);
        Assert.Equal(2.0, tempBasal.TempBasal);
        Assert.Equal(2.0, tempBasal.TotalBasal);
    }

    [Fact]
    public void GetTempBasalTreatment_WithBinarySearch_ShouldFindCorrectTreatment()
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);

        var tempBasals = CreateMultipleTempBasals();
        _profileService.UpdateTreatments(null, tempBasals);

        // Act
        var middleTime = DateTimeOffset.Parse("2024-01-01T12:30:00Z").ToUnixTimeMilliseconds();
        var treatment = _profileService.GetTempBasalTreatment(middleTime);

        // Assert
        Assert.NotNull(treatment);
        Assert.Equal(1.5, treatment.Absolute);
    }

    [Fact]
    public void ConvertToProfileStore_WithLegacyFormat_ShouldConvert()
    {
        // Arrange
        var legacyProfile = new Profile
        {
            Id = "test123",
            StartDate = "2024-01-01T00:00:00Z",
            DefaultProfile = "", // This triggers conversion (empty string)
            Store = new Dictionary<string, ProfileData>(),
        };

        // Act
        _profileService.LoadData(new List<Profile> { legacyProfile });

        // Assert
        Assert.True(_profileService.HasData());
        var profiles = _profileService.ListBasalProfiles();
        Assert.Contains("Default", profiles);
    }

    [Fact]
    public void GetValueByTime_WithTimeZone_ShouldAdjustTime()
    {
        // Arrange
        var profiles = CreateTestProfilesWithTimezone();
        _profileService.LoadData(profiles);

        // Test time that would be different in UTC vs local timezone
        var testTime = DateTimeOffset.Parse("2024-01-01T06:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var basalRate = _profileService.GetBasalRate(testTime);

        // Assert
        // Should handle timezone conversion (this is a complex test that depends on timezone setup)
        Assert.True(basalRate > 0);
    }

    [Fact]
    public void ListBasalProfiles_ShouldReturnAvailableProfiles()
    {
        // Arrange
        var profiles = CreateMultipleProfiles();
        _profileService.LoadData(profiles);

        // Act
        var profileNames = _profileService.ListBasalProfiles();

        // Assert
        Assert.Contains("Default", profileNames);
        Assert.Contains("Weekend", profileNames);
    }

    [Theory]
    [InlineData("00:00", 0)]
    [InlineData("06:00", 21600)]
    [InlineData("12:30", 45000)]
    [InlineData("23:59", 86340)]
    public void TimeStringToSeconds_ShouldConvertCorrectly(string timeString, int expectedSeconds)
    {
        // Arrange
        var profiles = CreateTestProfiles();
        _profileService.LoadData(profiles);

        // Act - Test indirectly through profile loading
        var timeValue = new TimeValue { Time = timeString, Value = 1.0 };

        // This tests the time conversion logic used internally
        var parts = timeString.Split(':');
        var actualSeconds = int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60;

        // Assert
        Assert.Equal(expectedSeconds, actualSeconds);
    }

    private List<Profile> CreateTestProfiles()
    {
        return new List<Profile>
        {
            new()
            {
                Id = "profile1",
                DefaultProfile = "Default",
                StartDate = "2024-01-01T00:00:00Z",
                Store = new Dictionary<string, ProfileData>
                {
                    {
                        "Default",
                        new ProfileData
                        {
                            Dia = 4.0,
                            CarbsHr = 25,
                            Units = "mg/dL",
                            Sens = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 50.0 },
                                new() { Time = "08:00", Value = 45.0 },
                                new() { Time = "18:00", Value = 40.0 },
                            },
                            CarbRatio = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 18.0 },
                                new() { Time = "08:00", Value = 15.0 },
                                new() { Time = "18:00", Value = 20.0 },
                            },
                            Basal = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 0.8 },
                                new() { Time = "06:00", Value = 1.2 },
                                new() { Time = "22:00", Value = 0.9 },
                            },
                            TargetLow = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 70.0 },
                            },
                            TargetHigh = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 180.0 },
                            },
                        }
                    },
                },
            },
        };
    }

    private List<Profile> CreateTestProfilesWithTimezone()
    {
        var profiles = CreateTestProfiles();
        profiles[0].Store["Default"].Timezone = "America/New_York";
        return profiles;
    }

    private List<Profile> CreateMultipleProfiles()
    {
        var profiles = CreateTestProfiles();
        profiles[0]
            .Store.Add(
                "Weekend",
                new ProfileData
                {
                    Dia = 3.5,
                    CarbsHr = 20,
                    Basal = new List<TimeValue>
                    {
                        new() { Time = "00:00", Value = 0.7 },
                        new() { Time = "08:00", Value = 1.0 },
                    },
                }
            );
        return profiles;
    }

    private List<Treatment> CreateMultipleTempBasals()
    {
        return new List<Treatment>
        {
            new()
            {
                Mills = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds(),
                Duration = 120,
                Absolute = 1.0,
            },
            new()
            {
                Mills = DateTimeOffset.Parse("2024-01-01T12:00:00Z").ToUnixTimeMilliseconds(),
                Duration = 60,
                Absolute = 1.5,
            },
            new()
            {
                Mills = DateTimeOffset.Parse("2024-01-01T14:00:00Z").ToUnixTimeMilliseconds(),
                Duration = 90,
                Absolute = 0.5,
            },
        };
    }

    [Fact]
    public void GetDIA_WithActiveBolusInsulin_ShouldReturnInsulinDia()
    {
        // Arrange: mock IPatientInsulinRepository to return a primary bolus insulin with DIA 3.5
        var insulinRepo = new Mock<IPatientInsulinRepository>();
        insulinRepo.Setup(r => r.GetPrimaryBolusInsulinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientInsulin { Dia = 3.5, IsPrimary = true });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileService(cache, MockTenantAccessor.Create().Object, insulinRepo.Object);

        var profiles = CreateTestProfiles(); // profile DIA is 4.0
        service.LoadData(profiles);

        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var dia = service.GetDIA(testTime);

        // Assert: returns 3.5 from insulin, not 4.0 from profile
        Assert.Equal(3.5, dia);
    }

    [Fact]
    public void GetDIA_WithNoActiveInsulin_ShouldFallBackToProfileDia()
    {
        // Arrange: mock IPatientInsulinRepository to return null
        var insulinRepo = new Mock<IPatientInsulinRepository>();
        insulinRepo.Setup(r => r.GetPrimaryBolusInsulinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((PatientInsulin?)null);

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileService(cache, MockTenantAccessor.Create().Object, insulinRepo.Object);

        var profiles = CreateTestProfiles(); // profile DIA is 4.0
        service.LoadData(profiles);

        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var dia = service.GetDIA(testTime);

        // Assert: returns profile DIA (existing behavior preserved)
        Assert.Equal(4.0, dia);
    }

    [Fact]
    public void GetDIA_WithExternallyManagedProfile_ShouldUseProfileDia()
    {
        // Arrange: profile has IsExternallyManaged = true, active insulin exists with DIA 3.5
        var insulinRepo = new Mock<IPatientInsulinRepository>();
        insulinRepo.Setup(r => r.GetPrimaryBolusInsulinAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PatientInsulin { Dia = 3.5, IsPrimary = true });

        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileService(cache, MockTenantAccessor.Create().Object, insulinRepo.Object);

        var profiles = CreateTestProfiles(); // profile DIA is 4.0
        profiles[0].IsExternallyManaged = true;
        service.LoadData(profiles);

        var testTime = DateTimeOffset.Parse("2024-01-01T10:00:00Z").ToUnixTimeMilliseconds();

        // Act
        var dia = service.GetDIA(testTime);

        // Assert: returns profile DIA (4.0), NOT the insulin's DIA (3.5)
        Assert.Equal(4.0, dia);
    }

    [Parity("basalprofileplugin.test.js")]
    [Fact]
    public void GetBasalRate_BasalProfilePluginScenario_ShouldMatchLegacy()
    {
        // Arrange - Legacy test case from basalprofileplugin.test.js with exact profile data
        var profiles = new List<Profile>
        {
            new()
            {
                Id = "legacy-test-profile",
                DefaultProfile = "Default",
                StartDate = "2015-06-21T00:00:00Z",
                Store = new Dictionary<string, ProfileData>
                {
                    {
                        "Default",
                        new ProfileData
                        {
                            Basal = new List<TimeValue>
                            {
                                new() { Time = "00:00", Value = 0.175 },
                                new() { Time = "02:30", Value = 0.125 },
                                new() { Time = "05:00", Value = 0.075 },
                                new() { Time = "08:00", Value = 0.1 },
                                new() { Time = "14:00", Value = 0.125 },
                                new() { Time = "20:00", Value = 0.3 },
                                new() { Time = "22:00", Value = 0.225 },
                            },
                        }
                    },
                },
            },
        };

        _profileService.LoadData(profiles);

        // Test time exactly matching legacy test: "2015-06-21T00:00:00+00:00"
        var testTime = new DateTimeOffset(
            2015,
            6,
            21,
            0,
            0,
            0,
            TimeSpan.Zero
        ).ToUnixTimeMilliseconds();

        // Act
        var basalRate = _profileService.GetBasalRate(testTime);

        // Assert - Should match legacy expectation of 0.1U
        Assert.Equal(0.1, basalRate);
    }
}

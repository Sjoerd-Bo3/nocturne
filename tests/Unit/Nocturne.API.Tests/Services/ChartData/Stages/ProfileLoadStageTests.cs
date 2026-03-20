using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.ChartData;
using Nocturne.API.Services.ChartData.Stages;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Tests.Services.ChartData.Stages;

public class ProfileLoadStageTests
{
    private readonly Mock<IProfileDataService> _profileDataService = new();
    private readonly Mock<IProfileService> _profileService = new();

    private ProfileLoadStage CreateStage() =>
        new(_profileDataService.Object, _profileService.Object, NullLogger<ProfileLoadStage>.Instance);

    private static ChartDataContext CreateContext(long endTime = 1700086400000L) =>
        new()
        {
            StartTime = 1700000000000L,
            EndTime = endTime,
            IntervalMinutes = 5,
            BufferStartTime = 1700000000000L - 8L * 60 * 60 * 1000,
        };

    [Fact]
    public async Task ExecuteAsync_WithProfiles_SeedsProfileServiceAndSetsContext()
    {
        // Arrange
        var endTime = 1700086400000L;
        var profiles = new List<Profile> { new() { Id = "p1" } };

        _profileDataService
            .Setup(s => s.GetProfilesAsync(null, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

        _profileService.Setup(s => s.HasData()).Returns(true);
        _profileService.Setup(s => s.GetTimezone(null)).Returns("America/New_York");
        _profileService.Setup(s => s.GetLowBGTarget(endTime, null)).Returns(80.0);
        _profileService.Setup(s => s.GetHighBGTarget(endTime, null)).Returns(160.0);
        _profileService.Setup(s => s.GetBasalRate(endTime, null)).Returns(0.8);

        var stage = CreateStage();
        var context = CreateContext(endTime);

        // Act
        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _profileService.Verify(s => s.LoadData(It.Is<List<Profile>>(l => l.Count == 1)), Times.Once);

        result.Timezone.Should().Be("America/New_York");
        result.Thresholds.VeryLow.Should().Be(54);
        result.Thresholds.Low.Should().Be(80.0);
        result.Thresholds.High.Should().Be(160.0);
        result.Thresholds.VeryHigh.Should().Be(250);
        result.DefaultBasalRate.Should().Be(0.8);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoProfiles_UsesDefaults()
    {
        // Arrange
        _profileDataService
            .Setup(s => s.GetProfilesAsync(null, 100, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Profile>)null!);

        _profileService.Setup(s => s.HasData()).Returns(false);

        var stage = CreateStage();
        var context = CreateContext();

        // Act
        var result = await stage.ExecuteAsync(context, CancellationToken.None);

        // Assert
        _profileService.Verify(s => s.LoadData(It.IsAny<List<Profile>>()), Times.Never);

        result.Timezone.Should().BeNull();
        result.Thresholds.VeryLow.Should().Be(54);
        result.Thresholds.Low.Should().Be(70);
        result.Thresholds.High.Should().Be(180);
        result.Thresholds.VeryHigh.Should().Be(250);
        result.DefaultBasalRate.Should().Be(1.0);
    }
}

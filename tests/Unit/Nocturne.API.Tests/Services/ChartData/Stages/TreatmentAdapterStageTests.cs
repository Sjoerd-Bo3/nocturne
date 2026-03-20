using FluentAssertions;
using Moq;
using Nocturne.API.Services.ChartData;
using Nocturne.API.Services.ChartData.Stages;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services.ChartData.Stages;

public class TreatmentAdapterStageTests
{
    private readonly Mock<ITreatmentFoodService> _mockTreatmentFoodService;
    private readonly TreatmentAdapterStage _stage;

    public TreatmentAdapterStageTests()
    {
        _mockTreatmentFoodService = new Mock<ITreatmentFoodService>();
        _stage = new TreatmentAdapterStage(_mockTreatmentFoodService.Object);
    }

    [Fact]
    public async Task ExecuteAsync_BuildsSyntheticTreatmentsAndFoodsByIntake()
    {
        // Arrange
        var bolusId = Guid.NewGuid();
        var carbIntakeId = Guid.NewGuid();

        var bolus = new Bolus
        {
            Id = bolusId,
            Timestamp = DateTime.UtcNow,
            Insulin = 3.5,
        };

        var carbIntake = new CarbIntake
        {
            Id = carbIntakeId,
            Timestamp = DateTime.UtcNow,
            Carbs = 45.0,
        };

        var treatmentFood = new TreatmentFood
        {
            Id = Guid.NewGuid(),
            CarbIntakeId = carbIntakeId,
            Portions = 1m,
            FatPerPortion = 10m,
            Carbs = 45m,
        };

        _mockTreatmentFoodService
            .Setup(s => s.GetByCarbIntakeIdsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([treatmentFood]);

        var context = new ChartDataContext
        {
            BolusList = [bolus],
            CarbIntakeList = [carbIntake],
        };

        // Act
        var result = await _stage.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.SyntheticTreatments.Should().HaveCount(2);
        result.SyntheticTreatments.Should().Contain(t => t.Insulin == 3.5);
        result.SyntheticTreatments.Should().Contain(t => t.Carbs == 45.0);

        result.FoodsByCarbIntake.Should().ContainKey(carbIntakeId);
        result.FoodsByCarbIntake[carbIntakeId].Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyData_ReturnsEmptyCollections()
    {
        // Arrange
        _mockTreatmentFoodService
            .Setup(s => s.GetByCarbIntakeIdsAsync(
                It.IsAny<IEnumerable<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var context = new ChartDataContext();

        // Act
        var result = await _stage.ExecuteAsync(context, CancellationToken.None);

        // Assert
        result.SyntheticTreatments.Should().BeEmpty();
        result.FoodsByCarbIntake.Should().BeEmpty();
    }
}

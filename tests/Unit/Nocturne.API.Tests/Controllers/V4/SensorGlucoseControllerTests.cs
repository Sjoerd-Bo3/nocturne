using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V4;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

[Trait("Category", "Unit")]
public class SensorGlucoseControllerTests
{
    private readonly Mock<ISensorGlucoseRepository> _repoMock = new();
    private readonly Mock<IAlertOrchestrator> _alertsMock = new();
    private readonly Mock<ILogger<SensorGlucoseController>> _loggerMock = new();

    private SensorGlucoseController CreateController()
    {
        var controller = new SensorGlucoseController(
            _repoMock.Object,
            _alertsMock.Object,
            _loggerMock.Object);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    [Fact]
    public async Task Create_CallsAlertOrchestrator_AfterCreate()
    {
        // Arrange
        var input = new SensorGlucose
        {
            Timestamp = DateTime.UtcNow,
            Mgdl = 120
        };

        var created = new SensorGlucose
        {
            Id = Guid.NewGuid(),
            Timestamp = input.Timestamp,
            Mgdl = 120
        };

        _repoMock
            .Setup(r => r.CreateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController();

        // Act
        var result = await controller.Create(input);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().Be(created);

        _alertsMock.Verify(
            a => a.EvaluateAndProcessSensorGlucoseAsync(
                It.Is<IEnumerable<SensorGlucose>>(readings => readings.Contains(created)),
                null,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_AlertOrchestratorThrows_StillReturns201()
    {
        // Arrange
        var input = new SensorGlucose
        {
            Timestamp = DateTime.UtcNow,
            Mgdl = 95
        };

        var created = new SensorGlucose
        {
            Id = Guid.NewGuid(),
            Timestamp = input.Timestamp,
            Mgdl = 95
        };

        _repoMock
            .Setup(r => r.CreateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        _alertsMock
            .Setup(a => a.EvaluateAndProcessSensorGlucoseAsync(
                It.IsAny<IEnumerable<SensorGlucose>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Alert service unavailable"));

        var controller = CreateController();

        // Act
        var result = await controller.Create(input);

        // Assert - should still return 201 despite alert failure
        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().Be(created);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<InvalidOperationException>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Create_AlertOrchestratorThrowsOperationCanceled_Rethrows()
    {
        // Arrange
        var input = new SensorGlucose
        {
            Timestamp = DateTime.UtcNow,
            Mgdl = 110
        };

        var created = new SensorGlucose
        {
            Id = Guid.NewGuid(),
            Timestamp = input.Timestamp,
            Mgdl = 110
        };

        _repoMock
            .Setup(r => r.CreateAsync(input, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        _alertsMock
            .Setup(a => a.EvaluateAndProcessSensorGlucoseAsync(
                It.IsAny<IEnumerable<SensorGlucose>>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var controller = CreateController();

        // Act & Assert - OperationCanceledException should NOT be swallowed
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => controller.Create(input));
    }
}

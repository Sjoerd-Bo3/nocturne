using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.ConnectorPublishing;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services.ConnectorPublishing;

[Trait("Category", "Unit")]
public class TreatmentPublisherTests
{
    private readonly Mock<ITreatmentService> _mockTreatmentService;
    private readonly Mock<IBolusRepository> _mockBolusRepository;
    private readonly Mock<ICarbIntakeRepository> _mockCarbIntakeRepository;
    private readonly Mock<IBGCheckRepository> _mockBGCheckRepository;
    private readonly Mock<IBolusCalculationRepository> _mockBolusCalculationRepository;
    private readonly Mock<ITempBasalRepository> _mockTempBasalRepository;
    private readonly TreatmentPublisher _publisher;

    public TreatmentPublisherTests()
    {
        _mockTreatmentService = new Mock<ITreatmentService>();
        _mockBolusRepository = new Mock<IBolusRepository>();
        _mockCarbIntakeRepository = new Mock<ICarbIntakeRepository>();
        _mockBGCheckRepository = new Mock<IBGCheckRepository>();
        _mockBolusCalculationRepository = new Mock<IBolusCalculationRepository>();
        _mockTempBasalRepository = new Mock<ITempBasalRepository>();

        _publisher = new TreatmentPublisher(
            _mockTreatmentService.Object,
            _mockBolusRepository.Object,
            _mockCarbIntakeRepository.Object,
            _mockBGCheckRepository.Object,
            _mockBolusCalculationRepository.Object,
            _mockTempBasalRepository.Object,
            NullLogger<TreatmentPublisher>.Instance
        );
    }

    [Fact]
    public async Task PublishTreatmentsAsync_DelegatesToTreatmentService()
    {
        var treatments = new List<Treatment> { new() { Id = "1" } };
        _mockTreatmentService
            .Setup(s => s.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatments);

        var result = await _publisher.PublishTreatmentsAsync(treatments, "test-source");

        result.Should().BeTrue();
        _mockTreatmentService.Verify(
            s => s.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishTreatmentsAsync_ReturnsFalse_OnException()
    {
        _mockTreatmentService
            .Setup(s => s.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        var result = await _publisher.PublishTreatmentsAsync(new List<Treatment>(), "test-source");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetLatestTreatmentTimestampAsync_ReturnsCreatedAt_WhenAvailable()
    {
        var createdAt = "2026-01-15T12:00:00Z";
        _mockTreatmentService
            .Setup(s => s.GetTreatmentsAsync(1, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Treatment> { new() { CreatedAt = createdAt } });

        var result = await _publisher.GetLatestTreatmentTimestampAsync("test-source");

        result.Should().Be(DateTime.Parse(createdAt));
    }

    [Fact]
    public async Task GetLatestTreatmentTimestampAsync_ReturnsTimestamp_WhenOnlyMillsSet()
    {
        // Treatment.CreatedAt auto-generates an ISO string from Mills,
        // so the CreatedAt parsing path is taken even when only Mills is set.
        var fixedTime = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);
        var mills = fixedTime.ToUnixTimeMilliseconds();
        _mockTreatmentService
            .Setup(s => s.GetTreatmentsAsync(1, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Treatment> { new() { Mills = mills } });

        var result = await _publisher.GetLatestTreatmentTimestampAsync("test-source");

        result.Should().NotBeNull();
        result!.Value.Date.Should().Be(new DateTime(2026, 1, 15));
    }

    [Fact]
    public async Task GetLatestTreatmentTimestampAsync_ReturnsNull_WhenNoTreatments()
    {
        _mockTreatmentService
            .Setup(s => s.GetTreatmentsAsync(1, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Treatment>());

        var result = await _publisher.GetLatestTreatmentTimestampAsync("test-source");

        result.Should().BeNull();
    }
}

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services.ConnectorPublishing;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Xunit;

namespace Nocturne.API.Tests.Services.ConnectorPublishing;

[Trait("Category", "Unit")]
public class GlucosePublisherTests
{
    private readonly Mock<IEntryService> _mockEntryService;
    private readonly Mock<ISensorGlucoseRepository> _mockSensorGlucoseRepository;
    private readonly GlucosePublisher _publisher;

    public GlucosePublisherTests()
    {
        _mockEntryService = new Mock<IEntryService>();
        _mockSensorGlucoseRepository = new Mock<ISensorGlucoseRepository>();

        _publisher = new GlucosePublisher(
            _mockEntryService.Object,
            _mockSensorGlucoseRepository.Object,
            Mock.Of<IDbContextFactory<NocturneDbContext>>(),
            Mock.Of<ITenantAccessor>(),
            Mock.Of<IAlertOrchestrator>(),
            NullLogger<GlucosePublisher>.Instance
        );
    }

    [Fact]
    public async Task PublishEntriesAsync_DelegatesToEntryService()
    {
        var entries = new List<Entry> { new() { Id = "1" } };
        _mockEntryService
            .Setup(s => s.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        var result = await _publisher.PublishEntriesAsync(entries, "test-source");

        result.Should().BeTrue();
        _mockEntryService.Verify(
            s => s.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishEntriesAsync_ReturnsFalse_OnException()
    {
        _mockEntryService
            .Setup(s => s.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        var result = await _publisher.PublishEntriesAsync(new List<Entry>(), "test-source");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetLatestEntryTimestampAsync_ReturnsDate_WhenEntryHasDate()
    {
        var expectedDate = new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);
        _mockEntryService
            .Setup(s => s.GetCurrentEntryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entry { Date = expectedDate });

        var result = await _publisher.GetLatestEntryTimestampAsync("test-source");

        result.Should().Be(expectedDate);
    }

    [Fact]
    public async Task GetLatestEntryTimestampAsync_ReturnsMills_WhenDateIsDefault()
    {
        var mills = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        _mockEntryService
            .Setup(s => s.GetCurrentEntryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Entry { Mills = mills });

        var result = await _publisher.GetLatestEntryTimestampAsync("test-source");

        result.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime);
    }

    [Fact]
    public async Task GetLatestEntryTimestampAsync_ReturnsNull_WhenNoEntry()
    {
        _mockEntryService
            .Setup(s => s.GetCurrentEntryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entry?)null);

        var result = await _publisher.GetLatestEntryTimestampAsync("test-source");

        result.Should().BeNull();
    }
}

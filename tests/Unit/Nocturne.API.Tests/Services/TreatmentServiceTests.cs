using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Treatments;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

[Parity("api.treatments.test.js")]
public class TreatmentServiceTests
{
    private readonly Mock<ITreatmentStore> _mockStore;
    private readonly Mock<ITreatmentCache> _mockCache;
    private readonly Mock<ITreatmentEventSink> _mockEvents;
    private readonly Mock<ILogger<TreatmentService>> _mockLogger;
    private readonly TreatmentService _treatmentService;

    public TreatmentServiceTests()
    {
        _mockStore = new Mock<ITreatmentStore>();
        _mockCache = new Mock<ITreatmentCache>();
        _mockEvents = new Mock<ITreatmentEventSink>();
        _mockLogger = new Mock<ILogger<TreatmentService>>();
        _treatmentService = new TreatmentService(_mockStore.Object, _mockCache.Object, _mockEvents.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetTreatmentsAsync_ShouldQueryStoreViaCache()
    {
        var expected = new List<Treatment> { new Treatment { Id = "1", EventType = "Meal Bolus" } };
        _mockCache.Setup(x => x.GetOrComputeAsync(It.IsAny<TreatmentQuery>(), It.IsAny<Func<Task<IReadOnlyList<Treatment>>>>(), It.IsAny<CancellationToken>()))
            .Returns<TreatmentQuery, Func<Task<IReadOnlyList<Treatment>>>, CancellationToken>(async (q, c, ct) => await c());
        _mockStore.Setup(x => x.QueryAsync(It.IsAny<TreatmentQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(expected.AsReadOnly());
        var result = await _treatmentService.GetTreatmentsAsync(count: 10, skip: 0, cancellationToken: CancellationToken.None);
        result.Should().ContainSingle();
    }

    [Fact]
    public async Task CreateTreatmentsAsync_ShouldInvalidateCacheAndPublishEvents()
    {
        var created = new List<Treatment> { new Treatment { Id = "1" } };
        _mockStore.Setup(x => x.CreateAsync(It.IsAny<IReadOnlyList<Treatment>>(), It.IsAny<CancellationToken>())).ReturnsAsync(created.AsReadOnly());
        var result = await _treatmentService.CreateTreatmentsAsync(new List<Treatment> { new Treatment() }, CancellationToken.None);
        result.Should().ContainSingle();
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockEvents.Verify(x => x.OnCreatedAsync(It.IsAny<IReadOnlyList<Treatment>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTreatmentAsync_ShouldInvalidateCacheAndPublishEvent()
    {
        var updated = new Treatment { Id = "id", EventType = "Meal Bolus" };
        _mockStore.Setup(x => x.UpdateAsync("id", It.IsAny<Treatment>(), It.IsAny<CancellationToken>())).ReturnsAsync(updated);
        var result = await _treatmentService.UpdateTreatmentAsync("id", new Treatment(), CancellationToken.None);
        result.Should().NotBeNull();
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockEvents.Verify(x => x.OnUpdatedAsync(updated, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTreatmentAsync_WhenNotFound_ShouldNotInvalidateOrPublish()
    {
        _mockStore.Setup(x => x.UpdateAsync("x", It.IsAny<Treatment>(), It.IsAny<CancellationToken>())).ReturnsAsync((Treatment?)null);
        var result = await _treatmentService.UpdateTreatmentAsync("x", new Treatment(), CancellationToken.None);
        result.Should().BeNull();
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTreatmentAsync_ShouldInvalidateCacheAndPublishEvent()
    {
        var existing = new Treatment { Id = "id" };
        _mockStore.Setup(x => x.GetByIdAsync("id", It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _mockStore.Setup(x => x.DeleteAsync("id", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var result = await _treatmentService.DeleteTreatmentAsync("id", CancellationToken.None);
        result.Should().BeTrue();
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockEvents.Verify(x => x.OnDeletedAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTreatmentAsync_WhenNotFound_ShouldNotInvalidateOrPublish()
    {
        _mockStore.Setup(x => x.GetByIdAsync("x", It.IsAny<CancellationToken>())).ReturnsAsync((Treatment?)null);
        _mockStore.Setup(x => x.DeleteAsync("x", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await _treatmentService.DeleteTreatmentAsync("x", CancellationToken.None);
        result.Should().BeFalse();
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteTreatmentsAsync_ShouldInvalidateCacheWhenDeleted()
    {
        _mockStore.Setup(x => x.BulkDeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(5);
        var result = await _treatmentService.DeleteTreatmentsAsync("q", CancellationToken.None);
        result.Should().Be(5);
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTreatmentsAsync_WhenNoneDeleted_ShouldNotInvalidateCache()
    {
        _mockStore.Setup(x => x.BulkDeleteAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        var result = await _treatmentService.DeleteTreatmentsAsync("q", CancellationToken.None);
        result.Should().Be(0);
        _mockCache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

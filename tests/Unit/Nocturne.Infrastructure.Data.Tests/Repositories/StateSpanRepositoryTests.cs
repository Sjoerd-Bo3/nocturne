using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests.Repositories;

[Trait("Category", "Unit")]
[Trait("Category", "Repository")]
public class StateSpanRepositoryTests : IDisposable
{
    private readonly NocturneDbContext _context;
    private readonly Mock<IDeduplicationService> _mockDedup;
    private readonly StateSpanRepository _repository;

    public StateSpanRepositoryTests()
    {
        _context = TestDbContextFactory.CreateInMemoryContext();
        _context.TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        _mockDedup = new Mock<IDeduplicationService>();
        _repository = new StateSpanRepository(
            _context, _mockDedup.Object, NullLogger<StateSpanRepository>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task UpsertStateSpanAsync_NewOverride_SupersedesExistingOpenOverride()
    {
        // Arrange - insert an open override span
        var existingSpan = new StateSpan
        {
            Category = StateSpanCategory.Override,
            State = OverrideState.Custom.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "nightscout",
            OriginalId = "old-override"
        };
        await _repository.UpsertStateSpanAsync(existingSpan);

        // Act - insert a new override span
        var newSpan = new StateSpan
        {
            Category = StateSpanCategory.Override,
            State = OverrideState.Custom.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "nightscout",
            OriginalId = "new-override"
        };
        var result = await _repository.UpsertStateSpanAsync(newSpan);

        // Assert - the old span should now be closed and superseded
        var allSpans = (await _repository.GetStateSpansAsync(
            category: StateSpanCategory.Override)).ToList();

        var oldSpan = allSpans.First(s => s.OriginalId == "old-override");
        oldSpan.EndTimestamp.Should().Be(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        oldSpan.SupersededById.Should().NotBeNullOrEmpty();
        oldSpan.IsActive.Should().BeFalse();

        var newSpanResult = allSpans.First(s => s.OriginalId == "new-override");
        newSpanResult.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpsertStateSpanAsync_NewTemporaryTarget_SupersedesExistingOpenTarget()
    {
        // Arrange
        var existingSpan = new StateSpan
        {
            Category = StateSpanCategory.TemporaryTarget,
            State = TemporaryTargetState.Active.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "AAPS",
            OriginalId = "old-target"
        };
        await _repository.UpsertStateSpanAsync(existingSpan);

        // Act
        var newSpan = new StateSpan
        {
            Category = StateSpanCategory.TemporaryTarget,
            State = TemporaryTargetState.Active.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "AAPS",
            OriginalId = "new-target"
        };
        await _repository.UpsertStateSpanAsync(newSpan);

        // Assert
        var allSpans = (await _repository.GetStateSpansAsync(
            category: StateSpanCategory.TemporaryTarget)).ToList();

        var oldSpan = allSpans.First(s => s.OriginalId == "old-target");
        oldSpan.EndTimestamp.Should().Be(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
        oldSpan.SupersededById.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task UpsertStateSpanAsync_NonExclusiveCategory_DoesNotSupersede()
    {
        // Arrange - Sleep is not an exclusive category
        var existingSpan = new StateSpan
        {
            Category = StateSpanCategory.Sleep,
            State = "Sleeping",
            StartTimestamp = new DateTime(2026, 1, 1, 22, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "manual",
            OriginalId = "sleep-1"
        };
        await _repository.UpsertStateSpanAsync(existingSpan);

        // Act - insert another sleep span
        var newSpan = new StateSpan
        {
            Category = StateSpanCategory.Sleep,
            State = "Sleeping",
            StartTimestamp = new DateTime(2026, 1, 2, 22, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "manual",
            OriginalId = "sleep-2"
        };
        await _repository.UpsertStateSpanAsync(newSpan);

        // Assert - both should remain open
        var allSpans = (await _repository.GetStateSpansAsync(
            category: StateSpanCategory.Sleep)).ToList();

        allSpans.Should().HaveCount(2);
        allSpans.Should().AllSatisfy(s => s.EndTimestamp.Should().BeNull());
        allSpans.Should().AllSatisfy(s => s.SupersededById.Should().BeNull());
    }

    [Fact]
    public async Task UpsertStateSpanAsync_UpdateExisting_DoesNotTriggerSupersession()
    {
        // Arrange - insert a span
        var span = new StateSpan
        {
            Category = StateSpanCategory.Override,
            State = OverrideState.Custom.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndTimestamp = null,
            Source = "nightscout",
            OriginalId = "override-to-update"
        };
        await _repository.UpsertStateSpanAsync(span);

        // Act - upsert again with same OriginalId (update path)
        var updatedSpan = new StateSpan
        {
            Category = StateSpanCategory.Override,
            State = OverrideState.Custom.ToString(),
            StartTimestamp = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            EndTimestamp = new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc),
            Source = "nightscout",
            OriginalId = "override-to-update"
        };
        await _repository.UpsertStateSpanAsync(updatedSpan);

        // Assert - should update in place, no supersession
        var allSpans = (await _repository.GetStateSpansAsync(
            category: StateSpanCategory.Override)).ToList();

        allSpans.Should().HaveCount(1);
        allSpans[0].SupersededById.Should().BeNull();
        allSpans[0].EndTimestamp.Should().Be(new DateTime(2026, 1, 1, 11, 0, 0, DateTimeKind.Utc));
    }
}

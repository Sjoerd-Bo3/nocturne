using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Entries;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for EntryService using Store/Cache/EventSink ports
/// </summary>
[Parity("api.entries.test.js")]
public class EntryServiceTests
{
    private readonly Mock<IEntryStore> _store = new();
    private readonly Mock<IEntryRepository> _repository = new();
    private readonly Mock<IEntryCache> _cache = new();
    private readonly Mock<IDataEventSink<Entry>> _events = new();
    private readonly EntryService _sut;

    public EntryServiceTests()
    {
        _sut = new EntryService(
            _store.Object,
            _repository.Object,
            _cache.Object,
            _events.Object,
            Mock.Of<ILogger<EntryService>>());
    }

    #region Read — Cache Hit

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesAsync_CacheHit_ReturnsFromCacheWithoutCallingStore()
    {
        // Arrange
        var cachedEntries = new List<Entry>
        {
            new() { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 },
            new() { Id = "2", Type = "sgv", Sgv = 110, Mills = 1234567880 },
        };

        _cache
            .Setup(x => x.GetOrComputeAsync(
                It.IsAny<EntryQuery>(),
                It.IsAny<Func<Task<IReadOnlyList<Entry>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntries);

        // Act
        var result = await _sut.GetEntriesAsync(cancellationToken: CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
        _store.Verify(
            x => x.QueryAsync(It.IsAny<EntryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesAsync_WithType_CacheHit_ReturnsFromCacheWithoutCallingStore()
    {
        // Arrange
        var cachedEntries = new List<Entry>
        {
            new() { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 },
        };

        _cache
            .Setup(x => x.GetOrComputeAsync(
                It.IsAny<EntryQuery>(),
                It.IsAny<Func<Task<IReadOnlyList<Entry>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedEntries);

        // Act
        var result = await _sut.GetEntriesAsync("sgv", 10, 0, CancellationToken.None);

        // Assert
        Assert.Single(result);
        _store.Verify(
            x => x.QueryAsync(It.IsAny<EntryQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Read — Cache Miss

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesAsync_CacheMiss_FallsBackToStore()
    {
        // Arrange
        var storeEntries = new List<Entry>
        {
            new() { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 },
        };

        _cache
            .Setup(x => x.GetOrComputeAsync(
                It.IsAny<EntryQuery>(),
                It.IsAny<Func<Task<IReadOnlyList<Entry>>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<Entry>?)null);

        _store
            .Setup(x => x.QueryAsync(It.IsAny<EntryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(storeEntries);

        // Act
        var result = await _sut.GetEntriesAsync(cancellationToken: CancellationToken.None);

        // Assert
        Assert.Single(result);
        _store.Verify(
            x => x.QueryAsync(It.IsAny<EntryQuery>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesAsync_WithParameters_BuildsCorrectQuery()
    {
        // Arrange
        var find = "{\"type\":\"sgv\"}";
        var expectedEntries = new List<Entry>
        {
            new() { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 },
        };

        // Cache delegates to the compute function so store is called
        _cache
            .Setup(x => x.GetOrComputeAsync(
                It.IsAny<EntryQuery>(),
                It.IsAny<Func<Task<IReadOnlyList<Entry>>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<EntryQuery, Func<Task<IReadOnlyList<Entry>>>, CancellationToken>(
                async (query, compute, ct) =>
                {
                    // Verify query was built correctly
                    Assert.Equal(find, query.Find);
                    Assert.Equal(10, query.Count);
                    Assert.Equal(0, query.Skip);
                    return await compute();
                });

        _store
            .Setup(x => x.QueryAsync(
                It.Is<EntryQuery>(q => q.Find == find && q.Count == 10 && q.Skip == 0),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntries);

        // Act — use named parameters to target the (find, count, skip) overload
        var result = await _sut.GetEntriesAsync(find: find, count: 10, skip: 0, cancellationToken: CancellationToken.None);

        // Assert
        Assert.Single(result);
    }

    #endregion

    #region GetById — Pure Delegation

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntryByIdAsync_WithValidId_DelegatesToStore()
    {
        // Arrange
        var entryId = "60a1b2c3d4e5f6789012345";
        var expectedEntry = new Entry { Id = entryId, Type = "sgv", Sgv = 120, Mills = 1234567890 };

        _store
            .Setup(x => x.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntry);

        // Act
        var result = await _sut.GetEntryByIdAsync(entryId, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(entryId, result.Id);
        _store.Verify(x => x.GetByIdAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntryByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        _store
            .Setup(x => x.GetByIdAsync("invalidid", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entry?)null);

        // Act
        var result = await _sut.GetEntryByIdAsync("invalidid", CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetCurrent — Uses Cache

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetCurrentEntryAsync_UsesCacheGetOrComputeCurrentAsync()
    {
        // Arrange
        var expectedEntry = new Entry { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 };

        _cache
            .Setup(x => x.GetOrComputeCurrentAsync(
                It.IsAny<Func<Task<Entry?>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntry);

        // Act
        var result = await _sut.GetCurrentEntryAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedEntry.Id, result.Id);
        _cache.Verify(
            x => x.GetOrComputeCurrentAsync(
                It.IsAny<Func<Task<Entry?>>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetCurrentEntryAsync_WithNoEntries_ReturnsNull()
    {
        // Arrange
        _cache
            .Setup(x => x.GetOrComputeCurrentAsync(
                It.IsAny<Func<Task<Entry?>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entry?)null);

        // Act
        var result = await _sut.GetCurrentEntryAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region AdvancedFilter — Pure Delegation (No Caching)

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesWithAdvancedFilterAsync_DelegatesToStore()
    {
        // Arrange
        var find = "{\"type\":\"sgv\",\"sgv\":{\"$gte\":100}}";
        var expectedEntries = new List<Entry>
        {
            new() { Id = "1", Type = "sgv", Sgv = 120, Mills = 1234567890 },
            new() { Id = "2", Type = "sgv", Sgv = 110, Mills = 1234567880 },
        };

        _store
            .Setup(x => x.QueryAsync(
                It.Is<EntryQuery>(q => q.Find == find && q.Count == 50 && q.Skip == 10),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntries);

        // Act
        var result = await _sut.GetEntriesWithAdvancedFilterAsync(find, 50, 10, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
        _cache.Verify(
            x => x.GetOrComputeAsync(
                It.IsAny<EntryQuery>(),
                It.IsAny<Func<Task<IReadOnlyList<Entry>>>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetEntriesWithAdvancedFilterAsync_WithFullParameterSet_PassesAllParams()
    {
        // Arrange
        var type = "sgv";
        var findQuery = "{\"sgv\":{\"$gte\":100}}";
        var dateString = "2022-01-01T12:00:00.000Z";
        var expectedEntries = new List<Entry> { new() { Id = "1" } };

        _store
            .Setup(x => x.QueryAsync(
                It.Is<EntryQuery>(q =>
                    q.Type == type &&
                    q.Find == findQuery &&
                    q.Count == 20 &&
                    q.Skip == 10 &&
                    q.DateString == dateString &&
                    q.ReverseResults == true),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedEntries);

        // Act
        var result = await _sut.GetEntriesWithAdvancedFilterAsync(
            type, 20, 10, findQuery, dateString, true, CancellationToken.None);

        // Assert
        Assert.Single(result);
    }

    #endregion

    #region Create

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateEntriesAsync_CallsStoreAndFiresEvent()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Type = "sgv", Sgv = 120, Mills = 1234567890 },
            new() { Type = "sgv", Sgv = 110, Mills = 1234567880 },
        };
        var createdEntries = entries.Select(e => new Entry
        {
            Id = Guid.NewGuid().ToString(), Type = e.Type, Sgv = e.Sgv, Mills = e.Mills
        }).ToList();

        _repository
            .Setup(x => x.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdEntries);

        // Act
        var result = await _sut.CreateEntriesAsync(entries, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Count());
        _repository.Verify(
            x => x.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        // Cache invalidation is handled by the EventSink, not the service
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnCreatedAsync(It.IsAny<IReadOnlyList<Entry>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateEntriesAsync_WhenStoreThrows_DoesNotInvalidateOrFireEvent()
    {
        // Arrange
        var entries = new List<Entry>
        {
            new() { Type = "sgv", Sgv = 120, Mills = 1234567890 },
        };

        _repository
            .Setup(x => x.CreateEntriesAsync(It.IsAny<IEnumerable<Entry>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateEntriesAsync(entries, CancellationToken.None));

        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnCreatedAsync(It.IsAny<IReadOnlyList<Entry>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Update

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateEntryAsync_WhenSuccessful_FiresEvent()
    {
        // Arrange
        var entryId = "60a1b2c3d4e5f6789012345";
        var entry = new Entry { Id = entryId, Type = "sgv", Sgv = 120, Mills = 1234567890 };
        var updatedEntry = new Entry { Id = entryId, Type = "sgv", Sgv = 125, Mills = 1234567890 };

        _repository
            .Setup(x => x.UpdateEntryAsync(entryId, entry, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedEntry);

        // Act
        var result = await _sut.UpdateEntryAsync(entryId, entry, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(125, result.Sgv);
        // Cache invalidation is handled by the EventSink, not the service
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnUpdatedAsync(It.IsAny<Entry>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateEntryAsync_WhenNotFound_DoesNotInvalidateOrFireEvent()
    {
        // Arrange
        var entry = new Entry { Type = "sgv", Sgv = 120, Mills = 1234567890 };

        _repository
            .Setup(x => x.UpdateEntryAsync("invalidid", entry, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entry?)null);

        // Act
        var result = await _sut.UpdateEntryAsync("invalidid", entry, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnUpdatedAsync(It.IsAny<Entry>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Delete

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteEntryAsync_WhenSuccessful_FullLifecycle()
    {
        // Arrange
        var entryId = "60a1b2c3d4e5f6789012345";
        var entryToDelete = new Entry { Id = entryId, Type = "sgv", Sgv = 120, Mills = 1234567890 };

        _store
            .Setup(x => x.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entryToDelete);
        _repository
            .Setup(x => x.DeleteEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteEntryAsync(entryId, CancellationToken.None);

        // Assert
        Assert.True(result);
        _events.Verify(
            x => x.BeforeDeleteAsync(entryId, It.IsAny<CancellationToken>()),
            Times.Once);
        _repository.Verify(x => x.DeleteEntryAsync(entryId, It.IsAny<CancellationToken>()), Times.Once);
        // Cache invalidation is handled by the EventSink, not the service
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnDeletedAsync(It.IsAny<Entry?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteEntryAsync_WhenNotFound_DoesNotInvalidateOrFireDeletedEvent()
    {
        // Arrange
        var entryId = "invalidid";

        _repository
            .Setup(x => x.DeleteEntryAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteEntryAsync(entryId, CancellationToken.None);

        // Assert
        Assert.False(result);
        _events.Verify(
            x => x.BeforeDeleteAsync(entryId, It.IsAny<CancellationToken>()),
            Times.Once);
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnDeletedAsync(It.IsAny<Entry?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region BulkDelete

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteEntriesAsync_CallsStoreAndFiresEvent()
    {
        // Arrange
        var find = "{\"type\":\"sgv\"}";

        _repository
            .Setup(x => x.BulkDeleteEntriesAsync(find, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5L);

        // Act
        var result = await _sut.DeleteEntriesAsync(find, CancellationToken.None);

        // Assert
        Assert.Equal(5, result);
        _repository.Verify(x => x.BulkDeleteEntriesAsync(find, It.IsAny<CancellationToken>()), Times.Once);
        // Cache invalidation is handled by the EventSink, not the service
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnBulkDeletedAsync(5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteEntriesAsync_WithNoMatches_StillFiresEvent()
    {
        // Arrange
        var find = "{\"type\":\"nonexistent\"}";

        _repository
            .Setup(x => x.BulkDeleteEntriesAsync(find, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0L);

        // Act
        var result = await _sut.DeleteEntriesAsync(find, CancellationToken.None);

        // Assert
        Assert.Equal(0, result);
        // Cache invalidation is handled by the EventSink, not the service
        _cache.Verify(x => x.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        _events.Verify(
            x => x.OnBulkDeletedAsync(0, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task DeleteEntriesAsync_WithNullFind_DefaultsToEmptyQuery()
    {
        // Arrange
        _repository
            .Setup(x => x.BulkDeleteEntriesAsync("{}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(3L);

        // Act
        var result = await _sut.DeleteEntriesAsync(null, CancellationToken.None);

        // Assert
        Assert.Equal(3, result);
        _repository.Verify(x => x.BulkDeleteEntriesAsync("{}", It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region CheckForDuplicate

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckForDuplicateEntryAsync_DelegatesToStore()
    {
        // Arrange
        _store
            .Setup(x => x.CheckDuplicateAsync("dev", "sgv", 120.0, 1234567890L, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Entry?)null);

        // Act
        var result = await _sut.CheckForDuplicateEntryAsync("dev", "sgv", 120.0, 1234567890L, 5, CancellationToken.None);

        // Assert
        Assert.Null(result);
        _store.Verify(
            x => x.CheckDuplicateAsync("dev", "sgv", 120.0, 1234567890L, 5, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CheckForDuplicateEntryAsync_WithDuplicate_ReturnsDuplicateEntry()
    {
        // Arrange
        var duplicateEntry = new Entry { Id = "dup-id", Device = "dev", Type = "sgv", Sgv = 120, Mills = 1234567830 };

        _store
            .Setup(x => x.CheckDuplicateAsync("dev", "sgv", 120.0, 1234567890L, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(duplicateEntry);

        // Act
        var result = await _sut.CheckForDuplicateEntryAsync("dev", "sgv", 120.0, 1234567890L, 5, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("dup-id", result.Id);
    }

    #endregion
}

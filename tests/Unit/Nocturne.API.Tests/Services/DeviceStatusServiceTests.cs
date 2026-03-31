using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Tests.Shared.Mocks;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for DeviceStatusService domain service with WebSocket broadcasting
/// </summary>
[Parity("api.devicestatus.test.js")]
public class DeviceStatusServiceTests
{
    private readonly Mock<IDeviceStatusRepository> _mockDeviceStatusRepository;
    private readonly Mock<IWriteSideEffects> _mockSideEffects;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<ILogger<DeviceStatusService>> _mockLogger;
    private readonly DeviceStatusService _deviceStatusService;

    public DeviceStatusServiceTests()
    {
        _mockDeviceStatusRepository = new Mock<IDeviceStatusRepository>();
        _mockSideEffects = new Mock<IWriteSideEffects>();
        _mockCacheService = new Mock<ICacheService>();
        _mockLogger = new Mock<ILogger<DeviceStatusService>>();

        _deviceStatusService = new DeviceStatusService(
            _mockDeviceStatusRepository.Object,
            _mockSideEffects.Object,
            Mock.Of<IDataEventSink<DeviceStatus>>(),
            _mockCacheService.Object,
            MockTenantAccessor.Create().Object,
            _mockLogger.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetDeviceStatusAsync_WithoutParameters_ReturnsAllDeviceStatus()
    {
        // Arrange
        var expectedDeviceStatus = new List<DeviceStatus>
        {
            new DeviceStatus
            {
                Id = "1",
                Device = "dexcom",
                Mills = 1234567890,
            },
            new DeviceStatus
            {
                Id = "2",
                Device = "loop",
                Mills = 1234567880,
            },
        };

        _mockCacheService
            .Setup(x =>
                x.GetAsync<IEnumerable<DeviceStatus>>(
                    "devicestatus:current:00000000-0000-0000-0000-000000000001",
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((IEnumerable<DeviceStatus>?)null);

        _mockDeviceStatusRepository
            .Setup(x => x.GetDeviceStatusAsync(10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDeviceStatus);

        // Act
        var result = await _deviceStatusService.GetDeviceStatusAsync(
            cancellationToken: CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal(expectedDeviceStatus, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetDeviceStatusAsync_WithParameters_ReturnsFilteredDeviceStatus()
    {
        // Arrange
        var find = "{\"device\":\"dexcom\"}";
        var count = 10;
        var skip = 0;
        var expectedDeviceStatus = new List<DeviceStatus>
        {
            new DeviceStatus
            {
                Id = "1",
                Device = "dexcom",
                Mills = 1234567890,
            },
        };

        // When find is provided, GetDeviceStatusWithAdvancedFilterAsync is called
        _mockDeviceStatusRepository
            .Setup(x => x.GetDeviceStatusWithAdvancedFilterAsync(
                count, skip, find, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDeviceStatus);

        // Act
        var result = await _deviceStatusService.GetDeviceStatusAsync(
            find,
            count,
            skip,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedDeviceStatus.First().Id, result.First().Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetDeviceStatusByIdAsync_WithValidId_ReturnsDeviceStatus()
    {
        // Arrange
        var deviceStatusId = "60a1b2c3d4e5f6789012345";
        var expectedDeviceStatus = new DeviceStatus
        {
            Id = deviceStatusId,
            Device = "dexcom",
            Mills = 1234567890,
        };

        _mockDeviceStatusRepository
            .Setup(x => x.GetDeviceStatusByIdAsync(deviceStatusId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDeviceStatus);

        // Act
        var result = await _deviceStatusService.GetDeviceStatusByIdAsync(
            deviceStatusId,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(deviceStatusId, result.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetDeviceStatusByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var deviceStatusId = "invalidid";

        _mockDeviceStatusRepository
            .Setup(x => x.GetDeviceStatusByIdAsync(deviceStatusId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeviceStatus?)null);

        // Act
        var result = await _deviceStatusService.GetDeviceStatusByIdAsync(
            deviceStatusId,
            CancellationToken.None
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateDeviceStatusAsync_WithValidDeviceStatus_ReturnsCreatedDeviceStatusAndBroadcasts()
    {
        // Arrange
        var deviceStatusEntries = new List<DeviceStatus>
        {
            new DeviceStatus { Device = "dexcom", Mills = 1234567890 },
            new DeviceStatus { Device = "loop", Mills = 1234567880 },
        };

        var createdDeviceStatus = deviceStatusEntries
            .Select(d => new DeviceStatus
            {
                Id = Guid.NewGuid().ToString(),
                Device = d.Device,
                Mills = d.Mills,
            })
            .ToList();

        _mockDeviceStatusRepository
            .Setup(x =>
                x.CreateDeviceStatusAsync(deviceStatusEntries, It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(createdDeviceStatus);

        // Act
        var result = await _deviceStatusService.CreateDeviceStatusAsync(
            deviceStatusEntries,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _mockDeviceStatusRepository.Verify(
            x => x.CreateDeviceStatusAsync(deviceStatusEntries, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnCreatedAsync(
                "devicestatus",
                It.IsAny<IReadOnlyList<DeviceStatus>>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateDeviceStatusAsync_WithValidDeviceStatus_ReturnsUpdatedDeviceStatusAndBroadcasts()
    {
        // Arrange
        var deviceStatusId = "60a1b2c3d4e5f6789012345";
        var deviceStatus = new DeviceStatus
        {
            Id = deviceStatusId,
            Device = "dexcom",
            Mills = 1234567890,
        };
        var updatedDeviceStatus = new DeviceStatus
        {
            Id = deviceStatusId,
            Device = "dexcom",
            Mills = 1234567900,
        };

        _mockDeviceStatusRepository
            .Setup(x =>
                x.UpdateDeviceStatusAsync(
                    deviceStatusId,
                    deviceStatus,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(updatedDeviceStatus);

        // Act
        var result = await _deviceStatusService.UpdateDeviceStatusAsync(
            deviceStatusId,
            deviceStatus,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(deviceStatusId, result.Id);
        Assert.Equal(1234567900, result.Mills);
        _mockDeviceStatusRepository.Verify(
            x =>
                x.UpdateDeviceStatusAsync(
                    deviceStatusId,
                    deviceStatus,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnUpdatedAsync(
                "devicestatus",
                It.IsAny<DeviceStatus>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateDeviceStatusAsync_WithInvalidId_ReturnsNullAndDoesNotBroadcast()
    {
        // Arrange
        var deviceStatusId = "invalidid";
        var deviceStatus = new DeviceStatus { Device = "dexcom", Mills = 1234567890 };

        _mockDeviceStatusRepository
            .Setup(x =>
                x.UpdateDeviceStatusAsync(
                    deviceStatusId,
                    deviceStatus,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((DeviceStatus?)null);

        // Act
        var result = await _deviceStatusService.UpdateDeviceStatusAsync(
            deviceStatusId,
            deviceStatus,
            CancellationToken.None
        );

        // Assert
        Assert.Null(result);
        _mockSideEffects.Verify(
            x => x.OnUpdatedAsync(
                It.IsAny<string>(),
                It.IsAny<DeviceStatus>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteDeviceStatusAsync_WithValidId_ReturnsTrueAndBroadcasts()
    {
        // Arrange
        var deviceStatusId = "60a1b2c3d4e5f6789012345";
        var deviceStatusToDelete = new DeviceStatus
        {
            Id = deviceStatusId,
            Device = "dexcom",
            Mills = 1234567890,
        };

        _mockDeviceStatusRepository
            .Setup(x => x.GetDeviceStatusByIdAsync(deviceStatusId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deviceStatusToDelete);

        _mockDeviceStatusRepository
            .Setup(x => x.DeleteDeviceStatusAsync(deviceStatusId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _deviceStatusService.DeleteDeviceStatusAsync(
            deviceStatusId,
            CancellationToken.None
        );

        // Assert
        Assert.True(result);
        _mockDeviceStatusRepository.Verify(
            x => x.DeleteDeviceStatusAsync(deviceStatusId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnDeletedAsync(
                "devicestatus",
                It.IsAny<DeviceStatus?>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteDeviceStatusAsync_WithInvalidId_ReturnsFalseAndDoesNotBroadcast()
    {
        // Arrange
        var deviceStatusId = "invalidid";

        _mockDeviceStatusRepository
            .Setup(x => x.DeleteDeviceStatusAsync(deviceStatusId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _deviceStatusService.DeleteDeviceStatusAsync(
            deviceStatusId,
            CancellationToken.None
        );

        // Assert
        Assert.False(result);
        _mockSideEffects.Verify(
            x => x.OnDeletedAsync(
                It.IsAny<string>(),
                It.IsAny<DeviceStatus?>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteDeviceStatusAsync_WithValidFilter_ReturnsDeletedCountAndCallsSideEffects()
    {
        // Arrange
        var find = "{\"device\":\"dexcom\"}";
        var deletedCount = 3L;

        _mockDeviceStatusRepository
            .Setup(x => x.BulkDeleteDeviceStatusAsync(find, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        var result = await _deviceStatusService.DeleteDeviceStatusEntriesAsync(
            find,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(deletedCount, result);
        _mockDeviceStatusRepository.Verify(
            x => x.BulkDeleteDeviceStatusAsync(find, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnBulkDeletedAsync(
                "devicestatus",
                deletedCount,
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteDeviceStatusAsync_WithNoMatches_CallsSideEffectsWithZero()
    {
        // Arrange
        var find = "{\"device\":\"nonexistent\"}";
        var deletedCount = 0L;

        _mockDeviceStatusRepository
            .Setup(x => x.BulkDeleteDeviceStatusAsync(find, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        var result = await _deviceStatusService.DeleteDeviceStatusEntriesAsync(
            find,
            CancellationToken.None
        );

        // Assert
        Assert.Equal(0, result);
        _mockSideEffects.Verify(
            x => x.OnBulkDeletedAsync(
                "devicestatus",
                0,
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateDeviceStatusAsync_WithException_ThrowsException()
    {
        // Arrange
        var deviceStatusEntries = new List<DeviceStatus>
        {
            new DeviceStatus { Device = "dexcom", Mills = 1234567890 },
        };

        _mockDeviceStatusRepository
            .Setup(x =>
                x.CreateDeviceStatusAsync(deviceStatusEntries, It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("Processing failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _deviceStatusService.CreateDeviceStatusAsync(
                deviceStatusEntries,
                CancellationToken.None
            )
        );
        _mockSideEffects.Verify(
            x => x.OnCreatedAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<DeviceStatus>>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetRecentDeviceStatusAsync_WithDeviceStatus_ReturnsLatestDeviceStatus()
    {
        // Arrange
        var expectedDeviceStatus = new List<DeviceStatus>
        {
            new DeviceStatus
            {
                Id = "1",
                Device = "dexcom",
                Mills = 1234567890,
            },
        };

        _mockDeviceStatusRepository
            .Setup(x =>
                x.GetDeviceStatusAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedDeviceStatus);

        // Act
        var result = await _deviceStatusService.GetRecentDeviceStatusAsync(
            cancellationToken: CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedDeviceStatus.First().Id, result.First().Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetRecentDeviceStatusAsync_WithNoDeviceStatus_ReturnsEmpty()
    {
        // Arrange
        var emptyDeviceStatus = new List<DeviceStatus>();

        _mockDeviceStatusRepository
            .Setup(x =>
                x.GetDeviceStatusAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(emptyDeviceStatus);

        // Act
        var result = await _deviceStatusService.GetRecentDeviceStatusAsync(
            cancellationToken: CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result);
    }
}

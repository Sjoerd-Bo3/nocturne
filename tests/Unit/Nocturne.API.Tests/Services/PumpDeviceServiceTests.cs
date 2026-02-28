using FluentAssertions;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for PumpDeviceService — resolves pump device identity from (pumpType, pumpSerial) pairs
/// </summary>
public class PumpDeviceServiceTests
{
    private readonly Mock<IPumpDeviceRepository> _mockRepository;
    private readonly PumpDeviceService _service;

    public PumpDeviceServiceTests()
    {
        _mockRepository = new Mock<IPumpDeviceRepository>();
        var mockTenantAccessor = new Mock<ITenantAccessor>();
        mockTenantAccessor.Setup(x => x.Context).Returns(new TenantContext(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), "test-tenant", "Test Tenant", true));
        mockTenantAccessor.Setup(x => x.IsResolved).Returns(true);
        mockTenantAccessor.Setup(x => x.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        _service = new PumpDeviceService(_mockRepository.Object, mockTenantAccessor.Object);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_NullPumpType_ReturnsNull()
    {
        // Act
        var result = await _service.ResolveAsync(null, "ABC123", 1000);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(
            r => r.FindByTypeAndSerialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_NullPumpSerial_ReturnsNull()
    {
        // Act
        var result = await _service.ResolveAsync("Omnipod DASH", null, 1000);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(
            r => r.FindByTypeAndSerialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_EmptyPumpType_ReturnsNull()
    {
        // Act
        var result = await _service.ResolveAsync("", "ABC123", 1000);

        // Assert
        result.Should().BeNull();
        _mockRepository.Verify(
            r => r.FindByTypeAndSerialAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_NewDevice_CreatesAndReturnsId()
    {
        // Arrange
        var pumpType = "Omnipod DASH";
        var pumpSerial = "ABC123";
        var mills = 1700000000000L;

        _mockRepository
            .Setup(r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PumpDevice?)null);

        _mockRepository
            .Setup(r => r.CreateAsync(It.IsAny<PumpDevice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PumpDevice device, CancellationToken _) => device);

        // Act
        var result = await _service.ResolveAsync(pumpType, pumpSerial, mills);

        // Assert
        result.Should().NotBeNull();
        _mockRepository.Verify(
            r => r.CreateAsync(
                It.Is<PumpDevice>(d =>
                    d.PumpType == pumpType &&
                    d.PumpSerial == pumpSerial &&
                    d.FirstSeenTimestamp == DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime &&
                    d.LastSeenTimestamp == DateTimeOffset.FromUnixTimeMilliseconds(mills).UtcDateTime),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_ExistingDevice_ReturnsExistingId()
    {
        // Arrange
        var pumpType = "Medtronic 780G";
        var pumpSerial = "XYZ789";
        var existingId = Guid.NewGuid();
        var mills = 1700000000000L;

        var existingDevice = new PumpDevice
        {
            Id = existingId,
            PumpType = pumpType,
            PumpSerial = pumpSerial,
            FirstSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1699000000000L).UtcDateTime,
            LastSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L).UtcDateTime
        };

        _mockRepository
            .Setup(r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _service.ResolveAsync(pumpType, pumpSerial, mills);

        // Assert
        result.Should().Be(existingId);
        _mockRepository.Verify(
            r => r.CreateAsync(It.IsAny<PumpDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_ExistingDevice_UpdatesLastSeenMills()
    {
        // Arrange
        var pumpType = "Omnipod DASH";
        var pumpSerial = "ABC123";
        var existingId = Guid.NewGuid();
        var olderMills = 1699000000000L;
        var newerMills = 1700000000000L;

        var existingDevice = new PumpDevice
        {
            Id = existingId,
            PumpType = pumpType,
            PumpSerial = pumpSerial,
            FirstSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1698000000000L).UtcDateTime,
            LastSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(olderMills).UtcDateTime
        };

        _mockRepository
            .Setup(r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        _mockRepository
            .Setup(r => r.UpdateAsync(existingId, It.IsAny<PumpDevice>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, PumpDevice d, CancellationToken _) => d);

        // Act
        var result = await _service.ResolveAsync(pumpType, pumpSerial, newerMills);

        // Assert
        result.Should().Be(existingId);
        _mockRepository.Verify(
            r => r.UpdateAsync(
                existingId,
                It.Is<PumpDevice>(d => d.LastSeenTimestamp == DateTimeOffset.FromUnixTimeMilliseconds(newerMills).UtcDateTime),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_ExistingDevice_DoesNotUpdateWhenMillsOlder()
    {
        // Arrange
        var pumpType = "Omnipod DASH";
        var pumpSerial = "ABC123";
        var existingId = Guid.NewGuid();
        var newerMills = 1700000000000L;
        var olderMills = 1699000000000L;

        var existingDevice = new PumpDevice
        {
            Id = existingId,
            PumpType = pumpType,
            PumpSerial = pumpSerial,
            FirstSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1698000000000L).UtcDateTime,
            LastSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(newerMills).UtcDateTime
        };

        _mockRepository
            .Setup(r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act
        var result = await _service.ResolveAsync(pumpType, pumpSerial, olderMills);

        // Assert
        result.Should().Be(existingId);
        _mockRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Guid>(), It.IsAny<PumpDevice>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAsync_SameDeviceTwice_UsesCacheSecondTime()
    {
        // Arrange
        var pumpType = "Omnipod DASH";
        var pumpSerial = "ABC123";
        var existingId = Guid.NewGuid();
        var mills = 1700000000000L;

        var existingDevice = new PumpDevice
        {
            Id = existingId,
            PumpType = pumpType,
            PumpSerial = pumpSerial,
            FirstSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1699000000000L).UtcDateTime,
            LastSeenTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000L).UtcDateTime
        };

        _mockRepository
            .Setup(r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDevice);

        // Act — call twice with same type/serial
        var result1 = await _service.ResolveAsync(pumpType, pumpSerial, mills);
        var result2 = await _service.ResolveAsync(pumpType, pumpSerial, mills);

        // Assert
        result1.Should().Be(existingId);
        result2.Should().Be(existingId);
        _mockRepository.Verify(
            r => r.FindByTypeAndSerialAsync(pumpType, pumpSerial, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

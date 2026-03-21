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
public class DevicePublisherTests
{
    private readonly Mock<IDeviceStatusService> _mockDeviceStatusService;
    private readonly Mock<IDeviceEventRepository> _mockDeviceEventRepository;
    private readonly DevicePublisher _publisher;

    public DevicePublisherTests()
    {
        _mockDeviceStatusService = new Mock<IDeviceStatusService>();
        _mockDeviceEventRepository = new Mock<IDeviceEventRepository>();

        _publisher = new DevicePublisher(
            _mockDeviceStatusService.Object,
            _mockDeviceEventRepository.Object,
            NullLogger<DevicePublisher>.Instance
        );
    }

    [Fact]
    public async Task PublishDeviceStatusAsync_DelegatesToDeviceStatusService()
    {
        var statuses = new List<DeviceStatus> { new() };

        var result = await _publisher.PublishDeviceStatusAsync(statuses, "test-source");

        result.Should().BeTrue();
        _mockDeviceStatusService.Verify(
            s => s.CreateDeviceStatusAsync(It.IsAny<IEnumerable<DeviceStatus>>(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task PublishDeviceStatusAsync_ReturnsFalse_OnException()
    {
        _mockDeviceStatusService
            .Setup(s => s.CreateDeviceStatusAsync(It.IsAny<IEnumerable<DeviceStatus>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("test error"));

        var result = await _publisher.PublishDeviceStatusAsync(new List<DeviceStatus>(), "test-source");

        result.Should().BeFalse();
    }
}

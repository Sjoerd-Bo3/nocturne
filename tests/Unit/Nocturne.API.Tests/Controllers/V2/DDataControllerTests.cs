using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V2;
using Nocturne.Core.Contracts.Legacy;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V2;

/// <summary>
/// Tests for V2 DDataController with 1:1 legacy compatibility
/// </summary>
public class DDataControllerTests
{
    private readonly Mock<IDDataService> _mockDDataService;
    private readonly Mock<ILogger<DDataController>> _mockLogger;
    private readonly DDataController _controller;

    public DDataControllerTests()
    {
        _mockDDataService = new Mock<IDDataService>();
        _mockLogger = new Mock<ILogger<DDataController>>();
        _controller = new DDataController(_mockDDataService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetDData_ShouldReturnOkResult()
    {
        // Arrange
        var expectedResponse = new DDataResponse
        {
            Sgvs = new List<Entry>(),
            Treatments = new List<Treatment>(),
            DeviceStatus = new List<DeviceStatus>(),
            Profiles = new List<Profile>(),
            Mbgs = new List<Entry>(),
            Cals = new List<Entry>(),
            Food = new List<Food>(),
            DbStats = new DbStats(),
        };

        _mockDDataService
            .Setup(x =>
                x.GetDDataWithRecentStatusesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetDData(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DDataResponse>(okResult.Value);
        Assert.NotNull(response);
        Assert.NotNull(response.Sgvs);
        Assert.NotNull(response.Treatments);
    }

    [Fact]
    public async Task GetDDataAt_WithValidTimestamp_ShouldReturnOkResult()
    {
        // Arrange
        var timestamp = "1640995200000"; // Valid Unix timestamp
        var expectedResponse = new DDataResponse();

        _mockDDataService
            .Setup(x =>
                x.GetDDataWithRecentStatusesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetDDataAt(timestamp, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<DDataResponse>(okResult.Value);
    }

    [Fact]
    public async Task GetDDataAt_WithValidIsoDate_ShouldReturnOkResult()
    {
        // Arrange
        var timestamp = "2022-01-01T00:00:00.000Z"; // Valid ISO date
        var expectedResponse = new DDataResponse();

        _mockDDataService
            .Setup(x =>
                x.GetDDataWithRecentStatusesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _controller.GetDDataAt(timestamp, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.IsType<DDataResponse>(okResult.Value);
    }

    [Fact]
    public async Task GetDDataAt_WithInvalidTimestamp_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidTimestamp = "invalid-timestamp";

        // Act
        var result = await _controller.GetDDataAt(invalidTimestamp, CancellationToken.None);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(badRequestResult.Value);
    }

    [Fact]
    public async Task GetRawDData_WithoutTimestamp_ShouldReturnCurrentData()
    {
        // Arrange
        var expectedData = new DData
        {
            LastUpdated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Sgvs = new List<Entry>(),
            Treatments = new List<Treatment>(),
        };

        _mockDDataService
            .Setup(x => x.GetDDataAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _controller.GetRawDData(cancellationToken: CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DData>(okResult.Value);
        Assert.NotNull(response);
        Assert.True(response.LastUpdated > 0);
    }

    [Fact]
    public async Task GetRawDData_WithTimestamp_ShouldReturnDataForTimestamp()
    {
        // Arrange
        var timestamp = "1640995200000";
        var expectedData = new DData { LastUpdated = 1640995200000 };

        _mockDDataService
            .Setup(x => x.GetDDataAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedData);

        // Act
        var result = await _controller.GetRawDData(timestamp, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<DData>(okResult.Value);
        Assert.NotNull(response);
    }

    [Fact]
    public async Task GetDData_WhenServiceThrows_ShouldReturn500()
    {
        // Arrange
        _mockDDataService
            .Setup(x =>
                x.GetDDataWithRecentStatusesAsync(It.IsAny<long>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await _controller.GetDData(CancellationToken.None);

        // Assert
        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}

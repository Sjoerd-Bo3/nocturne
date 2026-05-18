using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

[Trait("Category", "Unit")]
public class DeviceCatalogControllerTests
{
    [Fact]
    public void GetCatalog_ShouldReturnAllEntries()
    {
        var controller = new DeviceCatalogController();
        var result = controller.GetCatalog();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entries = okResult.Value.Should().BeAssignableTo<IReadOnlyList<DeviceCatalogEntry>>().Subject;
        entries.Should().NotBeEmpty();
    }

    [Fact]
    public void GetCatalogByCategory_ShouldFilterCorrectly()
    {
        var controller = new DeviceCatalogController();
        var result = controller.GetCatalogByCategory(DeviceCategory.CGM);
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entries = okResult.Value.Should().BeAssignableTo<IReadOnlyList<DeviceCatalogEntry>>().Subject;
        entries.Should().OnlyContain(e => e.Category == DeviceCategory.CGM);
    }
}

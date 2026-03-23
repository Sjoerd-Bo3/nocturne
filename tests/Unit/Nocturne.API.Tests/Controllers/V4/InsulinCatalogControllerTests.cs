using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4;
using Nocturne.Core.Models.V4;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

[Trait("Category", "Unit")]
public class InsulinCatalogControllerTests
{
    [Fact]
    public void GetCatalog_ShouldReturnAllFormulations()
    {
        var controller = new InsulinCatalogController();
        var result = controller.GetCatalog();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var formulations = okResult.Value.Should().BeAssignableTo<IReadOnlyList<InsulinFormulation>>().Subject;
        formulations.Should().NotBeEmpty();
    }

    [Fact]
    public void GetCatalogByCategory_ShouldFilterCorrectly()
    {
        var controller = new InsulinCatalogController();
        var result = controller.GetCatalogByCategory(InsulinCategory.RapidActing);
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var formulations = okResult.Value.Should().BeAssignableTo<IReadOnlyList<InsulinFormulation>>().Subject;
        formulations.Should().OnlyContain(f => f.Category == InsulinCategory.RapidActing);
    }
}

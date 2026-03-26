using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for browsing the static insulin formulation catalog.
/// </summary>
[ApiController]
[Route("api/v4/insulins")]
[Tags("V4 Insulins")]
[Authorize]
public class InsulinCatalogController : ControllerBase
{
    /// <summary>
    /// Get all known insulin formulations.
    /// </summary>
    [HttpGet("catalog")]
    [RemoteQuery]
    public ActionResult<IReadOnlyList<InsulinFormulation>> GetCatalog()
    {
        return Ok(InsulinCatalog.GetAll());
    }

    /// <summary>
    /// Get insulin formulations filtered by category.
    /// </summary>
    [HttpGet("catalog/{category}")]
    public ActionResult<IReadOnlyList<InsulinFormulation>> GetCatalogByCategory(InsulinCategory category)
    {
        return Ok(InsulinCatalog.GetByCategory(category));
    }
}

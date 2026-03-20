using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing CGM calibration records
/// </summary>
[ApiController]
[Route("api/v4/glucose/calibrations")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Calibrations")]
public class CalibrationController(ICalibrationRepository repo)
    : V4CrudControllerBase<Calibration, ICalibrationRepository>(repo)
{
    [ResponseCache(Duration = 120, VaryByQueryKeys = new[] { "*" })]
    public override Task<ActionResult<PaginatedResponse<Calibration>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
        => base.GetAll(from, to, limit, offset, sort, device, source, ct);
}

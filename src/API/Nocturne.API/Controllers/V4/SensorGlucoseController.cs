using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing CGM sensor glucose readings
/// </summary>
[ApiController]
[Route("api/v4/glucose/sensor")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Sensor Glucose")]
public class SensorGlucoseController(
    ISensorGlucoseRepository repo,
    IAlertOrchestrator alertOrchestrator,
    ILogger<SensorGlucoseController> logger)
    : V4CrudControllerBase<SensorGlucose, ISensorGlucoseRepository>(repo)
{
    [ResponseCache(Duration = 90, VaryByQueryKeys = new[] { "*" })]
    public override Task<ActionResult<PaginatedResponse<SensorGlucose>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
        => base.GetAll(from, to, limit, offset, sort, device, source, ct);

    protected override async Task<SensorGlucose> OnAfterCreateAsync(SensorGlucose created, CancellationToken ct)
    {
        try
        {
            if (created.Mgdl > 0)
            {
                var context = new SensorContext
                {
                    LatestValue = (decimal)created.Mgdl,
                    LatestTimestamp = created.Timestamp,
                    TrendRate = (decimal?)created.TrendRate,
                    LastReadingAt = created.Timestamp,
                };

                await alertOrchestrator.EvaluateAsync(context, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Alert evaluation failed after V4 SensorGlucose creation");
        }

        return created;
    }
}

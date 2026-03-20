using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.V4.Repositories;
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
    IAlertOrchestrator alerts,
    ILogger<SensorGlucoseController> logger)
    : V4CrudControllerBase<SensorGlucose, ISensorGlucoseRepository>(repo)
{
    protected override async Task<SensorGlucose> OnAfterCreateAsync(SensorGlucose created, CancellationToken ct)
    {
        try
        {
            await alerts.EvaluateAndProcessSensorGlucoseAsync([created], null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Alert evaluation failed for sensor glucose {Id}", created.Id);
        }

        return created;
    }
}

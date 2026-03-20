using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing blood glucose meter readings
/// </summary>
[ApiController]
[Route("api/v4/glucose/meter")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Meter Glucose")]
public class MeterGlucoseController(IMeterGlucoseRepository repo)
    : V4CrudControllerBase<MeterGlucose, IMeterGlucoseRepository>(repo);

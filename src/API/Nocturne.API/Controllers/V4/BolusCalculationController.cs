using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

[ApiController]
[Route("api/v4/insulin/calculations")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Bolus Calculations")]
public class BolusCalculationController(IBolusCalculationRepository repo)
    : V4CrudControllerBase<BolusCalculation, IBolusCalculationRepository>(repo);

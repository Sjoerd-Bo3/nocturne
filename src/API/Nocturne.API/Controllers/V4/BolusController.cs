using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

[ApiController]
[Route("api/v4/insulin/boluses")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Boluses")]
public class BolusController(IBolusRepository repo)
    : V4CrudControllerBase<Bolus, IBolusRepository>(repo);

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing blood glucose check observations
/// </summary>
[ApiController]
[Route("api/v4/observations/bg-checks")]
[Authorize]
[Produces("application/json")]
[Tags("V4 BG Checks")]
public class BGCheckController(IBGCheckRepository repo)
    : V4CrudControllerBase<BGCheck, IBGCheckRepository>(repo);

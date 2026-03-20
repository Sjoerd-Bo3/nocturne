using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing device event observations
/// </summary>
[ApiController]
[Route("api/v4/observations/device-events")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Device Events")]
public class DeviceEventController(IDeviceEventRepository repo)
    : V4CrudControllerBase<DeviceEvent, IDeviceEventRepository>(repo);

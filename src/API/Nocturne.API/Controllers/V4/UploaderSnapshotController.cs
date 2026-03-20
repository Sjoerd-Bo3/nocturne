using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Models.V4;
using Nocturne.Core.Contracts.V4.Repositories;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing uploader snapshot data
/// </summary>
[ApiController]
[Route("api/v4/device-status/uploader")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Uploader Snapshots")]
public class UploaderSnapshotController(IUploaderSnapshotRepository repo)
    : V4ReadOnlyControllerBase<UploaderSnapshot, IUploaderSnapshotRepository>(repo);

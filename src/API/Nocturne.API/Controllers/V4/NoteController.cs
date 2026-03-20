using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Controllers.V4.Base;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing note observations
/// </summary>
[ApiController]
[Route("api/v4/observations/notes")]
[Authorize]
[Produces("application/json")]
[Tags("V4 Notes")]
public class NoteController(INoteRepository repo)
    : V4CrudControllerBase<Note, INoteRepository>(repo);

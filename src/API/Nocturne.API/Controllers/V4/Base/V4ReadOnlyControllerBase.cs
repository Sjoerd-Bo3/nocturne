using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Base;

public abstract class V4ReadOnlyControllerBase<TModel, TRepository>(TRepository repository) : ControllerBase
    where TModel : class, IV4Record
    where TRepository : IV4Repository<TModel>
{
    protected TRepository Repository { get; } = repository;

    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<PaginatedResponse<TModel>>> GetAll(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int limit = 100, [FromQuery] int offset = 0,
        [FromQuery] string sort = "timestamp_desc",
        [FromQuery] string? device = null, [FromQuery] string? source = null,
        CancellationToken ct = default)
    {
        if (sort is not "timestamp_desc" and not "timestamp_asc")
            return Problem(detail: $"Invalid sort value '{sort}'. Must be 'timestamp_asc' or 'timestamp_desc'.", statusCode: 400, title: "Bad Request");

        var descending = sort == "timestamp_desc";
        var data = await Repository.GetAsync(from, to, device, source, limit, offset, descending, ct);
        var total = await Repository.CountAsync(from, to, ct);
        return Ok(new PaginatedResponse<TModel> { Data = data, Pagination = new PaginationInfo(limit, offset, total) });
    }

    [HttpGet("{id:guid}")]
    [RemoteQuery]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TModel>> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await Repository.GetByIdAsync(id, ct);
        return result is null ? NotFound() : Ok(result);
    }
}

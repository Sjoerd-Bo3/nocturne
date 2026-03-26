using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Base;

public abstract class V4CrudControllerBase<TModel, TCreateRequest, TUpdateRequest, TRepository>(TRepository repository)
    : V4ReadOnlyControllerBase<TModel, TRepository>(repository)
    where TModel : class, IV4Record
    where TCreateRequest : class
    where TUpdateRequest : class
    where TRepository : IV4Repository<TModel>
{
    protected abstract TModel MapCreateToModel(TCreateRequest request);
    protected abstract TModel MapUpdateToModel(Guid id, TUpdateRequest request, TModel existing);

    [HttpPost]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<TModel>> Create([FromBody] TCreateRequest request, CancellationToken ct = default)
    {
        var model = MapCreateToModel(request);

        if (model.Timestamp == default)
            return Problem(detail: "Timestamp must be set", statusCode: 400, title: "Bad Request");

        var created = await Repository.CreateAsync(model, ct);
        created = await OnAfterCreateAsync(created, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TModel>> Update(Guid id, [FromBody] TUpdateRequest request, CancellationToken ct = default)
    {
        var existing = await Repository.GetByIdAsync(id, ct);
        if (existing is null)
            return NotFound();

        var model = MapUpdateToModel(id, request, existing);

        if (model.Timestamp == default)
            return Problem(detail: "Timestamp must be set", statusCode: 400, title: "Bad Request");

        try
        {
            var updated = await Repository.UpdateAsync(id, model, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id:guid}")]
    [RemoteCommand]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        try
        {
            await Repository.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    protected virtual Task<TModel> OnAfterCreateAsync(TModel created, CancellationToken ct) => Task.FromResult(created);
}

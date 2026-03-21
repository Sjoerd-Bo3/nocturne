using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Controllers.V4.Base;

public abstract class V4CrudControllerBase<TModel, TRepository>(TRepository repository)
    : V4ReadOnlyControllerBase<TModel, TRepository>(repository)
    where TModel : class, IV4Record
    where TRepository : IV4Repository<TModel>
{
    [HttpPost]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<ActionResult<TModel>> Create([FromBody] TModel model, CancellationToken ct = default)
    {
        if (model.Timestamp == default)
            return BadRequest(new { error = "Timestamp must be set" });

        var created = await Repository.CreateAsync(model, ct);
        created = await OnAfterCreateAsync(created, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [RemoteForm]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public virtual async Task<ActionResult<TModel>> Update(Guid id, [FromBody] TModel model, CancellationToken ct = default)
    {
        if (model.Timestamp == default)
            return BadRequest(new { error = "Timestamp must be set" });

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

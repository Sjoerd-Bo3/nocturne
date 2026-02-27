using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Controllers.Admin;

[ApiController]
[Route("api/admin/tenants")]
[Authorize(Roles = "admin")]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public TenantController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _tenantService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tenant = await _tenantService.GetByIdAsync(id, ct);
        return tenant == null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.CreateAsync(request.Slug, request.DisplayName, request.ApiSecret, ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.UpdateAsync(id, request.DisplayName, request.IsActive, ct);
        return Ok(tenant);
    }

    [HttpPost("{id:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        await _tenantService.AddMemberAsync(id, request.SubjectId, request.Role, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{subjectId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid subjectId, CancellationToken ct)
    {
        await _tenantService.RemoveMemberAsync(id, subjectId, ct);
        return NoContent();
    }
}

public record CreateTenantRequest(string Slug, string DisplayName, string? ApiSecret = null);
public record UpdateTenantRequest(string DisplayName, bool IsActive);
public record AddMemberRequest(Guid SubjectId, string Role);

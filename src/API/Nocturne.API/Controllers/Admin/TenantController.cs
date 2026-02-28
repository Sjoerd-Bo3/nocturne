using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Controllers.Admin;

[ApiController]
[Route("api/admin/tenants")]
[Produces("application/json")]
[Authorize(Roles = "admin")]
public class TenantController : ControllerBase
{
    private readonly ITenantService _tenantService;
    private readonly ITenantMemberService _tenantMemberService;

    public TenantController(ITenantService tenantService, ITenantMemberService tenantMemberService)
    {
        _tenantService = tenantService;
        _tenantMemberService = tenantMemberService;
    }

    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<TenantDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _tenantService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    [RemoteQuery]
    [ProducesResponseType(typeof(TenantDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        var tenant = await _tenantService.GetByIdAsync(id, ct);
        return tenant == null ? NotFound() : Ok(tenant);
    }

    [HttpPost]
    [RemoteCommand(Invalidates = ["GetAll"])]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var tenant = await _tenantService.CreateAsync(request.Slug, request.DisplayName, request.ApiSecret, ct);
        return CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant);
    }

    [HttpPut("{id:guid}")]
    [RemoteCommand(Invalidates = ["GetAll", "GetById"])]
    [ProducesResponseType(typeof(TenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        var tenant = await _tenantService.UpdateAsync(id, request.DisplayName, request.IsActive, ct);
        return Ok(tenant);
    }

    [HttpPost("{id:guid}/members")]
    [RemoteCommand(Invalidates = ["GetById"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddMember(
        Guid id, [FromBody] AddMemberRequest request, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        await _tenantService.AddMemberAsync(id, request.SubjectId, request.Role, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{subjectId:guid}")]
    [RemoteCommand(Invalidates = ["GetById"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid subjectId, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        await _tenantService.RemoveMemberAsync(id, subjectId, ct);
        return NoContent();
    }

    /// <summary>
    /// Verifies the authenticated caller is a member of the specified tenant
    /// with the Owner role.
    /// </summary>
    private async Task<bool> IsCallerTenantOwnerAsync(Guid tenantId, CancellationToken ct)
    {
        var authContext = HttpContext.Items["AuthContext"] as AuthContext;
        if (authContext?.SubjectId is not { } subjectId)
            return false;

        var role = await _tenantMemberService.GetMemberRoleAsync(subjectId, tenantId, ct);
        return role == TenantRole.Owner;
    }
}

public record CreateTenantRequest(string Slug, string DisplayName, string? ApiSecret = null);
public record UpdateTenantRequest(string DisplayName, bool IsActive);
public record AddMemberRequest(Guid SubjectId, string Role);

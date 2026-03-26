using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
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
    private readonly IMemberInviteService _memberInviteService;

    public TenantController(
        ITenantService tenantService,
        ITenantMemberService tenantMemberService,
        IMemberInviteService memberInviteService)
    {
        _tenantService = tenantService;
        _tenantMemberService = tenantMemberService;
        _memberInviteService = memberInviteService;
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

    [HttpPost("{id:guid}/invites")]
    [RemoteCommand(Invalidates = ["GetById"])]
    [ProducesResponseType(typeof(MemberInviteResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateInvite(
        Guid id, [FromBody] CreateMemberInviteRequest request, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        var authContext = HttpContext.Items["AuthContext"] as AuthContext;
        var result = await _memberInviteService.CreateInviteAsync(
            id,
            authContext!.SubjectId!.Value,
            request.Role,
            request.Scopes,
            request.Label,
            request.ExpiresInDays,
            request.MaxUses,
            request.LimitTo24Hours);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("{id:guid}/invites")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<MemberInviteInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListInvites(Guid id, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        var invites = await _memberInviteService.GetInvitesForTenantAsync(id);
        return Ok(invites);
    }

    [HttpDelete("{id:guid}/invites/{inviteId:guid}")]
    [RemoteCommand(Invalidates = ["GetById"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeInvite(Guid id, Guid inviteId, CancellationToken ct)
    {
        if (!await IsCallerTenantOwnerAsync(id, ct))
            return Forbid();

        var revoked = await _memberInviteService.RevokeInviteAsync(inviteId, id);
        return revoked ? NoContent() : NotFound();
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

public class CreateMemberInviteRequest
{
    public string Role { get; set; } = "follower";
    public List<string>? Scopes { get; set; }
    public string? Label { get; set; }
    public int ExpiresInDays { get; set; } = 7;
    public int? MaxUses { get; set; }
    public bool LimitTo24Hours { get; set; }
}

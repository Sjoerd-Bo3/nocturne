using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.API.Extensions;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Public-facing member invite endpoints for accepting invites and listing members.
/// </summary>
[ApiController]
[Route("api/v4/member-invites")]
[Produces("application/json")]
[Tags("MemberInvites")]
public class MemberInviteController : ControllerBase
{
    private readonly IMemberInviteService _memberInviteService;
    private readonly ITenantService _tenantService;
    private readonly ITenantAccessor _tenantAccessor;

    public MemberInviteController(
        IMemberInviteService memberInviteService,
        ITenantService tenantService,
        ITenantAccessor tenantAccessor)
    {
        _memberInviteService = memberInviteService;
        _tenantService = tenantService;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// Get invite info for the accept page (anonymous).
    /// </summary>
    [HttpGet("{token}/info")]
    [AllowAnonymous]
    [RemoteQuery]
    [ProducesResponseType(typeof(MemberInviteInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInviteInfo(string token)
    {
        var invite = await _memberInviteService.GetInviteByTokenAsync(token);
        if (invite == null)
            return NotFound();

        return Ok(invite);
    }

    /// <summary>
    /// Accept an invite and join the tenant.
    /// </summary>
    [HttpPost("{token}/accept")]
    [Authorize]
    [RemoteCommand]
    [ProducesResponseType(typeof(AcceptMemberInviteResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvite(string token)
    {
        var subjectId = HttpContext.GetSubjectId();
        if (subjectId == null)
            return Unauthorized();

        var result = await _memberInviteService.AcceptInviteAsync(token, subjectId.Value);

        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// List all members of the current tenant.
    /// </summary>
    [HttpGet("members")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMembers(CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;
        var tenant = await _tenantService.GetByIdAsync(tenantId, ct);
        if (tenant == null)
            return NotFound();

        return Ok(tenant.Members);
    }

    /// <summary>
    /// List followers of the current tenant (members with follower role).
    /// </summary>
    [HttpGet("members/followers")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<TenantMemberDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFollowers(CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;
        var tenant = await _tenantService.GetByIdAsync(tenantId, ct);
        if (tenant == null)
            return NotFound();

        var followers = tenant.Members
            .Where(m => m.Role == TenantRole.Follower)
            .ToList();

        return Ok(followers);
    }
}

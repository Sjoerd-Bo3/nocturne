using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenApi.Remote.Attributes;
using Nocturne.API.Services.Chat;
using Nocturne.Core.Contracts.Multitenancy;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Manages chat platform identity links for bot-mediated alert delivery and glucose queries.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/chat-identity")]
[Tags("V4 Chat Identity")]
public class ChatIdentityController : ControllerBase
{
    private readonly ChatIdentityService _chatIdentityService;
    private readonly ITenantAccessor _tenantAccessor;

    public ChatIdentityController(
        ChatIdentityService chatIdentityService,
        ITenantAccessor tenantAccessor)
    {
        _chatIdentityService = chatIdentityService;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// List active chat identity links for the current tenant.
    /// </summary>
    [HttpGet]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<ChatIdentityLinkResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ChatIdentityLinkResponse>>> GetLinks(CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;

        var links = await _chatIdentityService.GetByTenantAsync(tenantId, ct);
        return Ok(links.Select(l => new ChatIdentityLinkResponse
        {
            Id = l.Id,
            NocturneUserId = l.NocturneUserId,
            Platform = l.Platform,
            PlatformUserId = l.PlatformUserId,
            PlatformChannelId = l.PlatformChannelId,
            DisplayUnit = l.DisplayUnit,
            IsActive = l.IsActive,
            CreatedAt = l.CreatedAt,
        }).ToList());
    }

    /// <summary>
    /// Create a new chat identity link.
    /// </summary>
    [HttpPost]
    [RemoteCommand(Invalidates = ["GetLinks"])]
    [ProducesResponseType(typeof(ChatIdentityLinkResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChatIdentityLinkResponse>> CreateLink(
        [FromBody] CreateChatIdentityLinkRequest request, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;

        var entity = await _chatIdentityService.CreateLinkAsync(
            tenantId,
            request.NocturneUserId,
            request.Platform,
            request.PlatformUserId,
            request.PlatformChannelId,
            ct);

        var response = new ChatIdentityLinkResponse
        {
            Id = entity.Id,
            NocturneUserId = entity.NocturneUserId,
            Platform = entity.Platform,
            PlatformUserId = entity.PlatformUserId,
            PlatformChannelId = entity.PlatformChannelId,
            DisplayUnit = entity.DisplayUnit,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
        };

        return CreatedAtAction(nameof(GetLinks), response);
    }

    /// <summary>
    /// Revoke (soft-delete) a chat identity link.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RemoteCommand(Invalidates = ["GetLinks"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RevokeLink(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;
        await _chatIdentityService.RevokeLinkAsync(tenantId, id, ct);
        return NoContent();
    }

    /// <summary>
    /// Resolve a platform identity to a Nocturne user. Used by the bot service
    /// to look up which tenant/user a chat message belongs to.
    /// </summary>
    [HttpGet("resolve")]
    [RemoteQuery]
    [ProducesResponseType(typeof(ChatIdentityLinkResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChatIdentityLinkResponse>> Resolve(
        [FromQuery] string platform, [FromQuery] string platformUserId, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;
        var link = await _chatIdentityService.FindByPlatformAsync(tenantId, platform, platformUserId, ct);

        if (link is null)
            return NotFound();

        return Ok(new ChatIdentityLinkResponse
        {
            Id = link.Id,
            NocturneUserId = link.NocturneUserId,
            Platform = link.Platform,
            PlatformUserId = link.PlatformUserId,
            PlatformChannelId = link.PlatformChannelId,
            DisplayUnit = link.DisplayUnit,
            IsActive = link.IsActive,
            CreatedAt = link.CreatedAt,
        });
    }
}

#region DTOs

public class ChatIdentityLinkResponse
{
    public Guid Id { get; set; }
    public Guid NocturneUserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string PlatformUserId { get; set; } = string.Empty;
    public string? PlatformChannelId { get; set; }
    public string DisplayUnit { get; set; } = "mg/dL";
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateChatIdentityLinkRequest
{
    public Guid NocturneUserId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string PlatformUserId { get; set; } = string.Empty;
    public string? PlatformChannelId { get; set; }
}

#endregion

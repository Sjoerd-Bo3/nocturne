using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nocturne.API.Extensions;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for managing alert invite links (create, validate, redeem, revoke).
/// </summary>
[ApiController]
[Route("api/v4/alert-invites")]
[Tags("V4 Alert Invites")]
public class AlertInvitesController : ControllerBase
{
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory;
    private readonly ILogger<AlertInvitesController> _logger;

    public AlertInvitesController(
        IDbContextFactory<NocturneDbContext> contextFactory,
        ILogger<AlertInvitesController> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Generate an invite link for a follower to join an escalation step.
    /// </summary>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(AlertInviteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AlertInviteResponse>> CreateInvite(
        [FromBody] CreateAlertInviteRequest request, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        // Verify the escalation step exists within this tenant
        var stepExists = await db.AlertEscalationSteps
            .AnyAsync(s => s.Id == request.EscalationStepId, ct);

        if (!stepExists)
            return BadRequest(new { error = "Escalation step not found" });

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId is null)
            return Unauthorized();

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var invite = new AlertInviteEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = db.TenantId,
            CreatedBy = subjectId.Value,
            Token = token,
            EscalationStepId = request.EscalationStepId,
            PermissionScope = request.PermissionScope ?? "view_acknowledge",
            IsUsed = false,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow,
        };

        db.AlertInvites.Add(invite);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(ValidateInvite), new { token = invite.Token }, new AlertInviteResponse
        {
            Id = invite.Id,
            Token = invite.Token,
            EscalationStepId = invite.EscalationStepId,
            PermissionScope = invite.PermissionScope,
            IsUsed = invite.IsUsed,
            ExpiresAt = invite.ExpiresAt,
            CreatedAt = invite.CreatedAt,
        });
    }

    /// <summary>
    /// Validate an invite token (public endpoint for redemption flow).
    /// </summary>
    [HttpGet("{token}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AlertInviteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult<AlertInviteResponse>> ValidateInvite(string token, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var invite = await db.AlertInvites
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Token == token, ct);

        if (invite is null)
            return NotFound();

        if (invite.IsUsed)
            return StatusCode(StatusCodes.Status410Gone, new { error = "Invite has already been redeemed" });

        if (invite.ExpiresAt < DateTime.UtcNow)
            return StatusCode(StatusCodes.Status410Gone, new { error = "Invite has expired" });

        return Ok(new AlertInviteResponse
        {
            Id = invite.Id,
            Token = invite.Token,
            EscalationStepId = invite.EscalationStepId,
            PermissionScope = invite.PermissionScope,
            IsUsed = invite.IsUsed,
            ExpiresAt = invite.ExpiresAt,
            CreatedAt = invite.CreatedAt,
        });
    }

    /// <summary>
    /// Redeem an invite token.
    /// </summary>
    [HttpPost("{token}/redeem")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status410Gone)]
    public async Task<ActionResult> RedeemInvite(string token, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var invite = await db.AlertInvites
            .FirstOrDefaultAsync(i => i.Token == token, ct);

        if (invite is null)
            return NotFound();

        if (invite.IsUsed)
            return StatusCode(StatusCodes.Status410Gone, new { error = "Invite has already been redeemed" });

        if (invite.ExpiresAt < DateTime.UtcNow)
            return StatusCode(StatusCodes.Status410Gone, new { error = "Invite has expired" });

        var subjectId = HttpContext.GetSubjectId();
        if (subjectId is null)
            return Unauthorized();

        invite.IsUsed = true;
        invite.UsedBy = subjectId.Value;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Revoke an unredeemed invite.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> RevokeInvite(Guid id, CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var invite = await db.AlertInvites
            .FirstOrDefaultAsync(i => i.Id == id, ct);

        if (invite is null)
            return NotFound();

        if (invite.IsUsed)
            return Conflict(new { error = "Cannot revoke an already-redeemed invite" });

        db.AlertInvites.Remove(invite);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}

#region DTOs

public class AlertInviteResponse
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public Guid EscalationStepId { get; set; }
    public string PermissionScope { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateAlertInviteRequest
{
    public Guid EscalationStepId { get; set; }
    public string? PermissionScope { get; set; }
}

#endregion

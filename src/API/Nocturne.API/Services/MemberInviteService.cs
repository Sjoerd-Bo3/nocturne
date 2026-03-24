using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

/// <summary>
/// Service for managing tenant membership invite links.
/// </summary>
public class MemberInviteService : IMemberInviteService
{
    private readonly NocturneDbContext _dbContext;
    private readonly IJwtService _jwtService;
    private readonly ILogger<MemberInviteService> _logger;
    private readonly OidcOptions _oidcOptions;

    /// <summary>
    /// Scopes that are allowed for follower grants (read-only access).
    /// </summary>
    private static readonly HashSet<string> AllowedFollowerScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        OAuthScopes.EntriesRead,
        OAuthScopes.TreatmentsRead,
        OAuthScopes.DeviceStatusRead,
        OAuthScopes.ProfileRead,
        OAuthScopes.NotificationsRead,
        OAuthScopes.ReportsRead,
        OAuthScopes.IdentityRead,
        OAuthScopes.HealthRead,
    };

    public MemberInviteService(
        NocturneDbContext dbContext,
        IJwtService jwtService,
        IOptions<OidcOptions> oidcOptions,
        ILogger<MemberInviteService> logger)
    {
        _dbContext = dbContext;
        _jwtService = jwtService;
        _oidcOptions = oidcOptions.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<MemberInviteResult> CreateInviteAsync(
        Guid tenantId,
        Guid createdBySubjectId,
        string role,
        List<string>? scopes = null,
        string? label = null,
        int expiresInDays = 7,
        int? maxUses = null,
        bool limitTo24Hours = false)
    {
        List<string>? resolvedScopes;

        if (role.Equals(TenantRole.Follower, StringComparison.OrdinalIgnoreCase))
        {
            // Validate scopes - only allow read scopes for followers
            if (scopes == null || scopes.Count == 0)
            {
                throw new ArgumentException("At least one scope is required for follower invites.");
            }

            var invalidScopes = scopes.Where(s => !AllowedFollowerScopes.Contains(s)).ToList();
            if (invalidScopes.Count > 0)
            {
                throw new ArgumentException(
                    $"Invalid scopes for follower invite: {string.Join(", ", invalidScopes)}. " +
                    "Only read-only scopes are allowed.");
            }

            resolvedScopes = scopes;
        }
        else
        {
            // Non-follower roles ignore scopes input
            resolvedScopes = null;
        }

        // Generate token
        var token = _jwtService.GenerateRefreshToken();
        var tokenHash = _jwtService.HashRefreshToken(token);

        var entity = new MemberInviteEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CreatedBySubjectId = createdBySubjectId,
            TokenHash = tokenHash,
            Role = role,
            Scopes = resolvedScopes,
            Label = label,
            LimitTo24Hours = limitTo24Hours,
            ExpiresAt = DateTime.UtcNow.AddDays(expiresInDays),
            MaxUses = maxUses,
            UseCount = 0,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.MemberInvites.Add(entity);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "MemberInviteAudit: {Event} invite_id={InviteId} tenant_id={TenantId} role={Role} expires_at={ExpiresAt}",
            "invite_created", entity.Id, tenantId, role, entity.ExpiresAt);

        // Build invite URL
        var baseUrl = _oidcOptions.BaseUrl?.TrimEnd('/') ?? "";
        var inviteUrl = $"{baseUrl}/invite/{token}";

        return new MemberInviteResult(
            entity.Id,
            token,
            inviteUrl,
            entity.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<MemberInviteInfo?> GetInviteByTokenAsync(string token)
    {
        if (string.IsNullOrEmpty(token))
            return null;

        var tokenHash = _jwtService.HashRefreshToken(token);

        var entity = await _dbContext.MemberInvites
            .Include(i => i.Tenant)
            .Include(i => i.CreatedBy)
            .Where(i => i.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        if (entity == null)
            return null;

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<AcceptMemberInviteResult> AcceptInviteAsync(string token, Guid acceptingSubjectId)
    {
        if (string.IsNullOrEmpty(token))
            return new AcceptMemberInviteResult(false, "invalid_token", "Invite token is required.");

        var tokenHash = _jwtService.HashRefreshToken(token);

        var entity = await _dbContext.MemberInvites
            .Include(i => i.Tenant)
            .Where(i => i.TokenHash == tokenHash)
            .FirstOrDefaultAsync();

        if (entity == null)
            return new AcceptMemberInviteResult(false, "invalid_token", "Invite not found or has been revoked.");

        if (entity.IsExpired)
            return new AcceptMemberInviteResult(false, "expired", "This invite has expired.");

        if (entity.IsRevoked)
            return new AcceptMemberInviteResult(false, "revoked", "This invite has been revoked.");

        if (entity.IsExhausted)
            return new AcceptMemberInviteResult(false, "exhausted", "This invite has reached its maximum uses.");

        // Check if already an active member of this tenant
        var existingMember = await _dbContext.TenantMembers
            .Where(m => m.TenantId == entity.TenantId
                        && m.SubjectId == acceptingSubjectId
                        && m.RevokedAt == null)
            .FirstOrDefaultAsync();

        if (existingMember != null)
            return new AcceptMemberInviteResult(false, "already_member", "You are already a member of this tenant.");

        // Create the tenant membership
        var member = new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = entity.TenantId,
            SubjectId = acceptingSubjectId,
            Role = entity.Role,
            Scopes = entity.Scopes,
            Label = entity.Label,
            LimitTo24Hours = entity.LimitTo24Hours,
            CreatedFromInviteId = entity.Id,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };

        _dbContext.TenantMembers.Add(member);

        // Increment use count
        entity.UseCount++;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "MemberInviteAudit: {Event} invite_id={InviteId} tenant_id={TenantId} subject_id={SubjectId} member_id={MemberId}",
            "invite_accepted", entity.Id, entity.TenantId, acceptingSubjectId, member.Id);

        return new AcceptMemberInviteResult(true, MembershipId: member.Id);
    }

    /// <inheritdoc />
    public async Task<List<MemberInviteInfo>> GetInvitesForTenantAsync(Guid tenantId)
    {
        var entities = await _dbContext.MemberInvites
            .Include(i => i.Tenant)
            .Include(i => i.CreatedBy)
            .Include(i => i.CreatedMembers)
                .ThenInclude(m => m.Subject)
            .Where(i => i.TenantId == tenantId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return entities.Select(MapToInfo).ToList();
    }

    /// <inheritdoc />
    public async Task<bool> RevokeInviteAsync(Guid inviteId, Guid tenantId)
    {
        var entity = await _dbContext.MemberInvites
            .Where(i => i.Id == inviteId && i.TenantId == tenantId)
            .FirstOrDefaultAsync();

        if (entity == null)
            return false;

        if (entity.RevokedAt.HasValue)
            return true; // Already revoked

        entity.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "MemberInviteAudit: {Event} invite_id={InviteId} tenant_id={TenantId}",
            "invite_revoked", inviteId, tenantId);

        return true;
    }

    private static MemberInviteInfo MapToInfo(MemberInviteEntity entity)
    {
        return new MemberInviteInfo(
            entity.Id,
            entity.TenantId,
            entity.Tenant?.DisplayName ?? "",
            entity.CreatedBy?.Name ?? "",
            entity.Role,
            entity.Scopes,
            entity.Label,
            entity.LimitTo24Hours,
            entity.ExpiresAt,
            entity.MaxUses,
            entity.UseCount,
            entity.IsValid,
            entity.IsExpired,
            entity.IsRevoked,
            entity.CreatedAt,
            entity.CreatedMembers
                .Where(m => m.RevokedAt == null)
                .Select(m => new InviteUsageInfo(
                    m.SubjectId,
                    m.Subject?.Name,
                    m.SysCreatedAt))
                .ToList());
    }
}

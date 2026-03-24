using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Service for managing OAuth authorization grants stored in the database.
/// </summary>
public class OAuthGrantService : IOAuthGrantService
{
    private readonly NocturneDbContext _dbContext;
    private readonly IOAuthClientService _clientService;
    private readonly ILogger<OAuthGrantService> _logger;

    /// <summary>
    /// Creates a new instance of OAuthGrantService
    /// </summary>
    public OAuthGrantService(
        NocturneDbContext dbContext,
        IOAuthClientService clientService,
        ILogger<OAuthGrantService> logger)
    {
        _dbContext = dbContext;
        _clientService = clientService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<OAuthGrantInfo> CreateOrUpdateGrantAsync(
        Guid clientEntityId,
        Guid subjectId,
        IEnumerable<string> scopes,
        string grantType = OAuthScopes.GrantTypeApp,
        string? label = null,
        CancellationToken ct = default)
    {
        var existingGrant = await _dbContext.OAuthGrants
            .Include(g => g.Client)
            .Where(g => g.ClientEntityId == clientEntityId
                     && g.SubjectId == subjectId
                     && g.RevokedAt == null)
            .FirstOrDefaultAsync(ct);

        if (existingGrant != null)
        {
            // Merge scopes: union of existing and new
            var mergedScopes = existingGrant.Scopes
                .Union(scopes)
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            existingGrant.Scopes = mergedScopes;

            if (label != null)
            {
                existingGrant.Label = label;
            }

            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation(
                "OAuthAudit: {Event} grant_id={GrantId} subject_id={SubjectId} client_entity_id={ClientEntityId} scopes={Scopes}",
                "grant_updated", existingGrant.Id, subjectId, clientEntityId, string.Join(" ", mergedScopes));

            return MapToInfo(existingGrant);
        }

        var scopeList = scopes.Distinct().OrderBy(s => s).ToList();

        var entity = new OAuthGrantEntity
        {
            Id = Guid.CreateVersion7(),
            ClientEntityId = clientEntityId,
            SubjectId = subjectId,
            GrantType = grantType,
            Scopes = scopeList,
            Label = label,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.OAuthGrants.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        // Load the Client navigation property for the return DTO
        await _dbContext.Entry(entity)
            .Reference(e => e.Client)
            .LoadAsync(ct);

        _logger.LogInformation(
            "OAuthAudit: {Event} grant_id={GrantId} subject_id={SubjectId} client_entity_id={ClientEntityId} scopes={Scopes}",
            "grant_created", entity.Id, subjectId, clientEntityId, string.Join(" ", scopeList));

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<OAuthGrantInfo?> GetActiveGrantAsync(
        Guid clientEntityId,
        Guid subjectId,
        CancellationToken ct = default)
    {
        var entity = await _dbContext.OAuthGrants
            .AsNoTracking()
            .Include(g => g.Client)
            .Where(g => g.ClientEntityId == clientEntityId
                     && g.SubjectId == subjectId
                     && g.RevokedAt == null)
            .FirstOrDefaultAsync(ct);

        if (entity == null)
        {
            return null;
        }

        return MapToInfo(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OAuthGrantInfo>> GetGrantsForSubjectAsync(
        Guid subjectId,
        CancellationToken ct = default)
    {
        var entities = await _dbContext.OAuthGrants
            .AsNoTracking()
            .Include(g => g.Client)
            .Where(g => g.SubjectId == subjectId && g.RevokedAt == null)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);

        return entities.Select(MapToInfo).ToList();
    }

    /// <inheritdoc />
    public async Task RevokeGrantAsync(Guid grantId, CancellationToken ct = default)
    {
        var grant = await _dbContext.OAuthGrants
            .Where(g => g.Id == grantId)
            .FirstOrDefaultAsync(ct);

        if (grant == null)
        {
            _logger.LogWarning("Attempted to revoke non-existent OAuth grant {GrantId}", grantId);
            return;
        }

        var now = DateTime.UtcNow;

        // Revoke the grant
        grant.RevokedAt = now;

        // Cascade revoke all associated OAuth refresh tokens
        var refreshTokens = await _dbContext.OAuthRefreshTokens
            .Where(t => t.GrantId == grantId && t.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in refreshTokens)
        {
            token.RevokedAt = now;
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "OAuthAudit: {Event} grant_id={GrantId} subject_id={SubjectId} revoked_tokens={TokenCount}",
            "grant_revoked", grantId, grant.SubjectId, refreshTokens.Count);
    }

    /// <inheritdoc />
    public async Task UpdateLastUsedAsync(
        Guid grantId,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default)
    {
        await _dbContext.OAuthGrants
            .Where(g => g.Id == grantId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.LastUsedAt, DateTime.UtcNow)
                .SetProperty(g => g.LastUsedIp, ipAddress)
                .SetProperty(g => g.LastUsedUserAgent, userAgent),
                ct);
    }

    /// <summary>
    /// Maps an OAuthGrantEntity to an OAuthGrantInfo DTO
    /// </summary>
    private static OAuthGrantInfo MapToInfo(OAuthGrantEntity entity)
    {
        return new OAuthGrantInfo
        {
            Id = entity.Id,
            ClientEntityId = entity.ClientEntityId,
            ClientId = entity.Client?.ClientId ?? string.Empty,
            ClientDisplayName = entity.Client?.DisplayName,
            IsKnownClient = entity.Client?.IsKnown ?? false,
            SubjectId = entity.SubjectId,
            GrantType = entity.GrantType,
            Scopes = entity.Scopes,
            Label = entity.Label,
            CreatedAt = entity.CreatedAt,
            LastUsedAt = entity.LastUsedAt,
            LastUsedIp = entity.LastUsedIp,
            LastUsedUserAgent = entity.LastUsedUserAgent,
            IsRevoked = entity.IsRevoked
        };
    }

    /// <inheritdoc />
    public async Task<OAuthGrantInfo?> UpdateGrantAsync(
        Guid grantId,
        Guid ownerSubjectId,
        string? label = null,
        IEnumerable<string>? scopes = null,
        CancellationToken ct = default)
    {
        var grant = await _dbContext.OAuthGrants
            .Include(g => g.Client)
            .Where(g => g.Id == grantId)
            .FirstOrDefaultAsync(ct);

        // Grant not found or not owned by the specified subject
        if (grant == null || grant.SubjectId != ownerSubjectId)
            return null;

        if (label != null)
        {
            grant.Label = label;
        }

        if (scopes != null)
        {
            var scopeList = scopes.ToList();
            grant.Scopes = scopeList.Distinct().OrderBy(s => s).ToList();
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "OAuthAudit: {Event} grant_id={GrantId} subject_id={SubjectId}",
            "grant_modified", grantId, ownerSubjectId);

        return MapToInfo(grant);
    }

}

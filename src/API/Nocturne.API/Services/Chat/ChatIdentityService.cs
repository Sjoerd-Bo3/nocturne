using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Chat;

/// <summary>
/// Manages chat platform identity links for bot-mediated interactions.
/// </summary>
public sealed class ChatIdentityService(
    IDbContextFactory<NocturneDbContext> contextFactory,
    ILogger<ChatIdentityService> logger)
{
    public async Task<ChatIdentityLinkEntity?> FindByPlatformAsync(
        Guid tenantId, string platform, string platformUserId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.TenantId = tenantId;

        return await db.ChatIdentityLinks
            .AsNoTracking()
            .Where(l => l.Platform == platform && l.PlatformUserId == platformUserId && l.IsActive)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatIdentityLinkEntity>> GetByUserAsync(
        Guid tenantId, Guid userId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.TenantId = tenantId;

        return await db.ChatIdentityLinks
            .AsNoTracking()
            .Where(l => l.NocturneUserId == userId && l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ChatIdentityLinkEntity>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.TenantId = tenantId;

        return await db.ChatIdentityLinks
            .AsNoTracking()
            .Where(l => l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ChatIdentityLinkEntity> CreateLinkAsync(
        Guid tenantId, Guid userId, string platform, string platformUserId,
        string? platformChannelId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.TenantId = tenantId;

        var entity = new ChatIdentityLinkEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            NocturneUserId = userId,
            Platform = platform,
            PlatformUserId = platformUserId,
            PlatformChannelId = platformChannelId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        db.ChatIdentityLinks.Add(entity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created chat identity link {LinkId} for user {UserId} on {Platform}",
            entity.Id, userId, platform);

        return entity;
    }

    public async Task RevokeLinkAsync(Guid tenantId, Guid linkId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        db.TenantId = tenantId;

        var link = await db.ChatIdentityLinks
            .FirstOrDefaultAsync(l => l.Id == linkId, ct);

        if (link is null) return;

        link.IsActive = false;
        link.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Revoked chat identity link {LinkId}", linkId);
    }
}

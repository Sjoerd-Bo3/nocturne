using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Chat;

/// <summary>
/// CRUD and routing service for the global chat identity directory. Handles
/// multi-link scenarios for a single platform user (one link per tenant),
/// label collision auto-suffixing, and transactional default-flag swaps.
/// </summary>
/// <remarks>
/// <para>
/// A single platform user (identified by platform + platformUserId) may be linked to multiple
/// Nocturne tenants. Each link has a human-readable label that must be unique within the
/// platform user's set of links. When the suggested label collides an integer suffix is
/// appended (e.g. <c>home-2</c>); up to 999 suffixes are tried before an exception is thrown.
/// </para>
/// <para>
/// The first link created for a platform user is automatically marked as default
/// (<c>IsDefault = true</c>). Subsequent calls to <see cref="SetDefaultAsync"/> clear the
/// previous default and set the new one atomically inside a user-initiated transaction wrapped
/// by the Npgsql retry execution strategy, enabling safe replay on transient failures.
/// </para>
/// <para>
/// Each method opens its own <see cref="Microsoft.EntityFrameworkCore.DbContext"/> from the
/// factory to stay compatible with the transactional retry pattern and avoid concurrency issues
/// with shared contexts.
/// </para>
/// </remarks>
/// <seealso cref="ChatIdentityService"/>
/// <seealso cref="ChatIdentityPendingLinkService"/>
public sealed class ChatIdentityDirectoryService(
    IDbContextFactory<NocturneDbContext> contextFactory,
    ILogger<ChatIdentityDirectoryService> logger)
{
    /// <summary>Returns all active directory entries for a platform user, ordered by creation date.</summary>
    public async Task<IReadOnlyList<ChatIdentityDirectoryEntry>> GetCandidatesAsync(
        string platform, string platformUserId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.ChatIdentityDirectory
            .Where(d => d.Platform == platform
                        && d.PlatformUserId == platformUserId
                        && d.IsActive)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>Resolves a single directory entry by platform user and optional label, falling back to the default link.</summary>
    public async Task<ChatIdentityDirectoryEntry?> GetByPlatformAndUserAsync(
        string platform, string platformUserId, string? labelArg, CancellationToken ct)
    {
        var candidates = await GetCandidatesAsync(platform, platformUserId, ct);
        if (candidates.Count == 0)
        {
            return null;
        }

        if (labelArg is not null)
        {
            return candidates.FirstOrDefault(c => c.Label == labelArg);
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var defaults = candidates.Where(c => c.IsDefault).ToList();
        return defaults.Count == 1 ? defaults[0] : null;
    }

    /// <summary>Returns all active directory entries for a tenant.</summary>
    public async Task<IReadOnlyList<ChatIdentityDirectoryEntry>> GetByTenantAsync(
        Guid tenantId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.ChatIdentityDirectory
            .Where(d => d.TenantId == tenantId && d.IsActive)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>Returns a directory entry by its ID, or null if not found.</summary>
    public async Task<ChatIdentityDirectoryEntry?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        return await db.ChatIdentityDirectory.FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    /// <summary>Creates a directory link between a chat platform user and a tenant, auto-suffixing the label if it collides.</summary>
    public async Task<ChatIdentityDirectoryEntry> CreateLinkAsync(
        string platform, string platformUserId, Guid tenantId, Guid nocturneUserId,
        string suggestedLabel, string suggestedDisplayName, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);

        var existing = await db.ChatIdentityDirectory
            .FirstOrDefaultAsync(d => d.Platform == platform
                                      && d.PlatformUserId == platformUserId
                                      && d.TenantId == tenantId, ct);
        if (existing is not null)
        {
            return existing;
        }

        var existingLabels = await db.ChatIdentityDirectory
            .Where(d => d.Platform == platform && d.PlatformUserId == platformUserId)
            .Select(d => d.Label)
            .ToListAsync(ct);

        var resolvedLabel = ResolveUniqueLabel(existingLabels, suggestedLabel);
        var isFirst = existingLabels.Count == 0;

        var entry = new ChatIdentityDirectoryEntry
        {
            Platform = platform,
            PlatformUserId = platformUserId,
            TenantId = tenantId,
            NocturneUserId = nocturneUserId,
            Label = resolvedLabel,
            DisplayName = suggestedDisplayName,
            IsDefault = isFirst,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        db.ChatIdentityDirectory.Add(entry);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Created chat identity directory link {LinkId} for {Platform}:{PlatformUserId} -> tenant {TenantId} with label '{Label}' (default={IsDefault})",
            entry.Id, platform, platformUserId, tenantId, resolvedLabel, isFirst);

        return entry;
    }

    /// <summary>Designates a link as the default for the platform user, clearing the previous default in a transaction.</summary>
    public async Task SetDefaultAsync(Guid linkId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var target = await db.ChatIdentityDirectory.FirstOrDefaultAsync(d => d.Id == linkId, ct)
            ?? throw new InvalidOperationException($"Chat identity directory link {linkId} not found");

        // NpgsqlRetryingExecutionStrategy requires user-initiated transactions
        // to be wrapped in strategy.ExecuteAsync so the entire block can be
        // retried as a unit on transient failures.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.ChatIdentityDirectory
                .Where(d => d.Platform == target.Platform
                            && d.PlatformUserId == target.PlatformUserId
                            && d.Id != target.Id
                            && d.IsDefault)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.IsDefault, false), ct);

            target.IsDefault = true;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        logger.LogInformation(
            "Set chat identity directory link {LinkId} as default for {Platform}:{PlatformUserId}",
            linkId, target.Platform, target.PlatformUserId);
    }

    /// <summary>Renames a link's label, throwing if the new label collides with an existing one.</summary>
    public async Task RenameLabelAsync(Guid linkId, string newLabel, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var target = await db.ChatIdentityDirectory.FirstOrDefaultAsync(d => d.Id == linkId, ct)
            ?? throw new InvalidOperationException($"Chat identity directory link {linkId} not found");

        target.Label = newLabel;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            logger.LogWarning(ex,
                "Label rename collision: link {LinkId} could not be renamed to '{NewLabel}'",
                linkId, newLabel);
            throw new InvalidOperationException(
                $"Label '{newLabel}' is already in use", ex);
        }
    }

    /// <summary>Updates the display name shown to the chat platform user for a link.</summary>
    public async Task UpdateDisplayNameAsync(Guid linkId, string newDisplayName, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        var target = await db.ChatIdentityDirectory.FirstOrDefaultAsync(d => d.Id == linkId, ct)
            ?? throw new InvalidOperationException($"Chat identity directory link {linkId} not found");

        target.DisplayName = newDisplayName;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Permanently deletes a directory link.</summary>
    public async Task RevokeAsync(Guid linkId, CancellationToken ct)
    {
        await using var db = await contextFactory.CreateDbContextAsync(ct);
        await db.ChatIdentityDirectory
            .Where(d => d.Id == linkId)
            .ExecuteDeleteAsync(ct);
    }

    private static string ResolveUniqueLabel(
        IReadOnlyCollection<string> existingLabels, string suggested)
    {
        if (!existingLabels.Contains(suggested))
        {
            return suggested;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{suggested}-{i}";
            if (!existingLabels.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not resolve unique label after 1000 attempts");
    }
}

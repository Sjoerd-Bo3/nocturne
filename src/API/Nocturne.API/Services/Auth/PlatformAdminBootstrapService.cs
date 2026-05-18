using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Ensures at least one platform admin exists on startup.
/// </summary>
/// <remarks>
/// Priority order:
/// <list type="number">
///   <item>If <c>Platform:AdminSubjectIds</c> is configured, those subjects are granted platform admin status.</item>
///   <item>Otherwise, if no platform admin exists, the owner of the oldest tenant is granted it.</item>
/// </list>
/// </remarks>
public class PlatformAdminBootstrapService
{
    private readonly NocturneDbContext _db;
    private readonly PlatformOptions _options;

    /// <summary>
    /// Initialises a new <see cref="PlatformAdminBootstrapService"/>.
    /// </summary>
    /// <param name="db">Database context for subject and tenant member queries.</param>
    /// <param name="options">Platform configuration options, including <c>AdminSubjectIds</c>.</param>
    public PlatformAdminBootstrapService(NocturneDbContext db, IOptions<PlatformOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    /// <summary>
    /// Grants platform admin status according to the configured priority rules.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task BootstrapAsync(CancellationToken cancellationToken)
    {
        // Option 1: explicit config takes precedence
        if (_options.AdminSubjectIds.Count > 0)
        {
            await _db.Subjects
                .Where(s => _options.AdminSubjectIds.Contains(s.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlatformAdmin, true), cancellationToken);
            return;
        }

        // No-op if a platform admin already exists
        if (await _db.Subjects.AnyAsync(s => s.IsPlatformAdmin, cancellationToken))
            return;

        // Option 2: grant to owner of oldest tenant
        var firstOwnerSubjectId = await _db.TenantMembers
            .Where(tm => tm.MemberRoles.Any(mr => mr.TenantRole!.Slug == "owner"))
            .OrderBy(tm => tm.Tenant!.SysCreatedAt)
            .Select(tm => tm.SubjectId)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstOwnerSubjectId == default) return;

        await _db.Subjects
            .Where(s => s.Id == firstOwnerSubjectId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsPlatformAdmin, true), cancellationToken);
    }
}

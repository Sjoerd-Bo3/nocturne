using Microsoft.EntityFrameworkCore;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

public class TenantService : ITenantService
{
    private readonly IDbContextFactory<NocturneDbContext> _factory;

    public TenantService(IDbContextFactory<NocturneDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<TenantDto> CreateAsync(
        string slug, string displayName, string? apiSecret = null, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);

        var tenant = new TenantEntity
        {
            Slug = slug.ToLowerInvariant(),
            DisplayName = displayName,
            ApiSecretHash = apiSecret != null ? HashUtils.Sha1Hex(apiSecret) : null,
            IsActive = true,
        };

        context.Tenants.Add(tenant);
        await context.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    public async Task<List<TenantDto>> GetAllAsync(CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        return await context.Tenants.AsNoTracking()
            .Select(t => new TenantDto(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt))
            .ToListAsync(ct);
    }

    public async Task<TenantDetailDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var tenant = await context.Tenants.AsNoTracking()
            .Include(t => t.Members)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant == null) return null;

        return new TenantDetailDto(
            tenant.Id, tenant.Slug, tenant.DisplayName, tenant.IsActive, tenant.IsDefault, tenant.SysCreatedAt,
            tenant.Members.Select(m => new TenantMemberDto(m.SubjectId, m.Role, m.SysCreatedAt)).ToList());
    }

    public async Task<TenantDto> UpdateAsync(
        Guid id, string displayName, bool isActive, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var tenant = await context.Tenants.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"Tenant {id} not found");

        tenant.DisplayName = displayName;
        tenant.IsActive = isActive;
        await context.SaveChangesAsync(ct);
        return ToDto(tenant);
    }

    public async Task AddMemberAsync(
        Guid tenantId, Guid subjectId, string role, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        context.TenantMembers.Add(new TenantMemberEntity
        {
            TenantId = tenantId,
            SubjectId = subjectId,
            Role = role,
        });
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(
        Guid tenantId, Guid subjectId, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        var member = await context.TenantMembers
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.SubjectId == subjectId, ct);

        if (member != null)
        {
            context.TenantMembers.Remove(member);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task<List<TenantDto>> GetTenantsForSubjectAsync(
        Guid subjectId, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);
        return await context.TenantMembers.AsNoTracking()
            .Where(tm => tm.SubjectId == subjectId)
            .Include(tm => tm.Tenant)
            .Select(tm => new TenantDto(
                tm.Tenant!.Id, tm.Tenant.Slug, tm.Tenant.DisplayName,
                tm.Tenant.IsActive, tm.Tenant.IsDefault, tm.Tenant.SysCreatedAt))
            .ToListAsync(ct);
    }

    private static TenantDto ToDto(TenantEntity t) =>
        new(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt);
}

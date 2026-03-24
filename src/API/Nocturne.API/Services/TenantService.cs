using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Nocturne.API.Multitenancy;
using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

public partial class TenantService : ITenantService
{
    private readonly IDbContextFactory<NocturneDbContext> _factory;
    private readonly IMemoryCache _cache;
    private readonly MultitenancyConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "www", "default", "app", "mail", "ftp",
        "status", "help", "support"
    };

    [GeneratedRegex(@"^[a-z0-9][a-z0-9\-]{1,62}[a-z0-9]$")]
    private static partial Regex SlugPattern();

    public TenantService(
        IDbContextFactory<NocturneDbContext> factory,
        IMemoryCache cache,
        IOptions<MultitenancyConfiguration> config,
        IHttpClientFactory httpClientFactory)
    {
        _factory = factory;
        _cache = cache;
        _config = config.Value;
        _httpClientFactory = httpClientFactory;
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
                .ThenInclude(m => m.Subject)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

        if (tenant == null) return null;

        return new TenantDetailDto(
            tenant.Id, tenant.Slug, tenant.DisplayName, tenant.IsActive, tenant.IsDefault, tenant.SysCreatedAt,
            tenant.Members.Select(m => new TenantMemberDto(m.SubjectId, m.Subject?.Name, m.Role, m.SysCreatedAt)).ToList());
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

        // Invalidate cached tenant context
        _cache.Remove($"tenant:{tenant.Slug}");
        if (tenant.IsDefault)
            _cache.Remove("tenant:__default__");

        return ToDto(tenant);
    }

    public async Task AddMemberAsync(
        Guid tenantId, Guid subjectId, string role, CancellationToken ct = default)
    {
        await using var context = await _factory.CreateDbContextAsync(ct);

        // Check if already a member
        var exists = await context.TenantMembers
            .AnyAsync(tm => tm.TenantId == tenantId && tm.SubjectId == subjectId, ct);

        if (exists)
            return;

        context.TenantMembers.Add(new TenantMemberEntity
        {
            TenantId = tenantId,
            SubjectId = subjectId,
            Role = role,
        });

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Race condition: another request already inserted. This is fine.
        }
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

    public async Task<SlugValidationResult> ValidateSlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.ToLowerInvariant().Trim();

        if (!SlugPattern().IsMatch(normalized))
            return new SlugValidationResult(false, "Slug must be 3-64 characters, alphanumeric and hyphens only, no leading/trailing hyphens");

        if (ReservedSlugs.Contains(normalized))
            return new SlugValidationResult(false, "This name is reserved");

        await using var context = await _factory.CreateDbContextAsync(ct);
        var exists = await context.Tenants.AsNoTracking()
            .AnyAsync(t => t.Slug == normalized, ct);

        if (exists)
            return new SlugValidationResult(false, "This name is already taken");

        if (!string.IsNullOrEmpty(_config.SlugValidationWebhookUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("slug-validation");
                var response = await client.PostAsJsonAsync(
                    _config.SlugValidationWebhookUrl,
                    new { slug = normalized },
                    ct);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<SlugValidationResult>(ct);
                    if (result is { IsValid: false })
                        return result;
                }
            }
            catch
            {
                // Webhook failure should not block validation — fall through to success
            }
        }

        return new SlugValidationResult(true);
    }

    private static TenantDto ToDto(TenantEntity t) =>
        new(t.Id, t.Slug, t.DisplayName, t.IsActive, t.IsDefault, t.SysCreatedAt);
}

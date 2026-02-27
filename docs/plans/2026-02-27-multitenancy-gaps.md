# Multitenancy Gaps Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Close all remaining multitenancy gaps so every service, background job, and cache key correctly scopes data to the resolved tenant.

**Architecture:** The tenant infrastructure is already in place — `TenantContext`, `ITenantAccessor`, EF global query filters, RLS policies, and `TenantResolutionMiddleware` all work correctly. The gaps are in services that predate the multitenancy work and still use hardcoded tenant IDs, missing per-tenant iteration in background services, a broken unique index, cache invalidation, and inconsistent role strings.

**Tech Stack:** .NET 10, Entity Framework Core, PostgreSQL, xUnit + Moq + FluentAssertions

---

## Progress

**Task 1 is DONE** — committed as `7a8c5204`. Start from Task 2.

---

## Critical build/test notes

- **NSwag hangs:** The NSwag post-build step can hang indefinitely. ALWAYS use `-p:GenerateNSwagClient=false` on `dotnet build` commands. ALWAYS prefix `dotnet test` with `timeout 120`.
- **Aspire file locks:** If Aspire is running, `dotnet build` may fail with MSB3021 file lock errors. Stop Aspire before running EF migrations or full solution builds.
- **Test timeout pattern:** `timeout 120 dotnet test tests/Unit/Nocturne.API.Tests --filter "..." --no-build -v minimal`

---

## Context for the implementing agent

### How tenant context flows

1. **HTTP requests:** `TenantResolutionMiddleware` resolves the tenant from the subdomain (or default tenant) and calls `ITenantAccessor.SetTenant()`. The scoped `NocturneDbContext` reads `ITenantAccessor.TenantId` at checkout time for query filters. `TenantConnectionInterceptor` sets `app.current_tenant_id` on the PostgreSQL session for RLS.

2. **Background services:** Must create a fresh `IServiceScope` per tenant, resolve `ITenantAccessor` from that scope, and call `SetTenant()` before doing any work. See `ConnectorBackgroundService.SyncForTenantAsync()` at `src/API/Nocturne.API/Services/BackgroundServices/ConnectorBackgroundService.cs:240-290` for the canonical pattern.

3. **Cache keys:** `CacheKeyBuilder` already accepts a `tenantId` string parameter on every method. The three services with the bug pass `"default"` instead of the real tenant slug.

### Key files you'll modify repeatedly

- `src/API/Nocturne.API/Services/EntryService.cs`
- `src/API/Nocturne.API/Services/TreatmentService.cs`
- `src/API/Nocturne.API/Services/ProfileDataService.cs`
- `src/API/Nocturne.API/Services/BackgroundServices/DeviceHealthMonitoringService.cs`
- `src/API/Nocturne.API/Services/BackgroundServices/CompressionLowDetectionService.cs`
- `src/API/Nocturne.API/Services/NotificationResolutionService.cs`
- `src/API/Nocturne.API/Services/TenantService.cs`
- `src/API/Nocturne.API/Multitenancy/TenantResolutionMiddleware.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TenantMemberEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/SystemEventEntity.cs`

### Running tests

ALWAYS use `timeout` to prevent hangs:

```bash
timeout 120 dotnet test tests/Unit/Nocturne.API.Tests --filter "Category!=Integration&Category!=Performance" --no-build -v minimal
```

Known pre-existing failures (ignore these): FoodControllerTests (2), ActivityControllerTests (2), StatusServiceTests (3), LocalIdentityServicePasswordResetTests (1).

### Building

ALWAYS disable NSwag to prevent hangs:

```bash
dotnet build src/API/Nocturne.API -p:GenerateNSwagClient=false
dotnet build src/Infrastructure/Nocturne.Infrastructure.Data -p:GenerateNSwagClient=false
```

### EF Migrations

Stop Aspire before running migrations. NSwag runs as a post-build step and must be disabled:

```bash
dotnet build src/Infrastructure/Nocturne.Infrastructure.Data -p:GenerateNSwagClient=false
dotnet ef migrations add <MigrationName> --project src/Infrastructure/Nocturne.Infrastructure.Data --startup-project src/API/Nocturne.API --no-build
```

---

## Task 1: Fix cache key tenant collision in EntryService, TreatmentService, ProfileDataService

These three services use `private const string DefaultTenantId = "default"` for all cache keys. This means all tenants share the same cache — a data leak.

**Files:**
- Modify: `src/API/Nocturne.API/Services/EntryService.cs`
- Modify: `src/API/Nocturne.API/Services/TreatmentService.cs`
- Modify: `src/API/Nocturne.API/Services/ProfileDataService.cs`

**Step 1: Add `ITenantAccessor` to EntryService constructor**

In `EntryService.cs`, add `ITenantAccessor` to the constructor parameters and store it as a field. Replace the `DefaultTenantId` constant with a property that reads from the accessor:

```csharp
// Add to fields (replacing the DefaultTenantId const):
private readonly ITenantAccessor _tenantAccessor;

// Add to constructor parameter list:
ITenantAccessor tenantAccessor

// Add to constructor body:
_tenantAccessor = tenantAccessor;

// Add helper property:
private string TenantSlug => _tenantAccessor.Context?.Slug ?? "default";
```

Then replace every occurrence of `DefaultTenantId` with `TenantSlug` throughout the file. There are occurrences on lines 74, 159, 456, 547, 633, and 700.

Add `using Nocturne.Core.Contracts.Multitenancy;` to the imports.

**Step 2: Do the same for TreatmentService**

Same pattern: add `ITenantAccessor` to constructor, replace `DefaultTenantId` constant with `TenantSlug` property, replace all usages. Search for `DefaultTenantId` in the file to find all occurrences.

**Step 3: Do the same for ProfileDataService**

Same pattern.

**Step 4: Build and run tests**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 5: Commit**

```bash
git add src/API/Nocturne.API/Services/EntryService.cs src/API/Nocturne.API/Services/TreatmentService.cs src/API/Nocturne.API/Services/ProfileDataService.cs
git commit -m "fix: use real tenant ID in cache keys instead of hardcoded default

EntryService, TreatmentService, and ProfileDataService all used a
hardcoded 'default' string for cache key generation, causing all
tenants to share the same cache namespace. Inject ITenantAccessor
and use the resolved tenant slug."
```

---

## Task 2: Fix connector_configurations unique index to be composite with tenant_id

The unique index on `connector_name` alone prevents multiple tenants from each configuring the same connector (e.g., Dexcom).

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs` (line ~1178)
- Create: new EF migration

**Step 1: Change the index in NocturneDbContext**

At line 1178-1182, change:

```csharp
modelBuilder
    .Entity<ConnectorConfigurationEntity>()
    .HasIndex(c => c.ConnectorName)
    .HasDatabaseName("ix_connector_configurations_connector_name")
    .IsUnique();
```

To:

```csharp
modelBuilder
    .Entity<ConnectorConfigurationEntity>()
    .HasIndex(c => new { c.ConnectorName, c.TenantId })
    .HasDatabaseName("ix_connector_configurations_connector_name_tenant")
    .IsUnique();
```

**Step 2: Generate EF migration**

```bash
dotnet build src/Infrastructure/Nocturne.Infrastructure.Data -p:GenerateNSwagClient=false
dotnet ef migrations add FixConnectorConfigUniqueIndex --project src/Infrastructure/Nocturne.Infrastructure.Data --startup-project src/API/Nocturne.API --no-build
```

**Step 3: Verify the migration looks correct**

Read the generated migration file and confirm it drops the old index and creates the new composite one.

**Step 4: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 5: Commit**

```bash
git add src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs src/Infrastructure/Nocturne.Infrastructure.Data/Migrations/
git commit -m "fix: make connector_configurations unique index composite with tenant_id

The unique index on connector_name alone prevented multiple tenants
from each configuring the same connector type."
```

---

## Task 3: Add per-tenant iteration to DeviceHealthMonitoringService

This service creates a single scope without setting tenant context, making it effectively broken in multitenant mode (queries with `TenantId = Guid.Empty` return nothing).

**Files:**
- Modify: `src/API/Nocturne.API/Services/BackgroundServices/DeviceHealthMonitoringService.cs`

**Step 1: Add tenant iteration to PerformHealthCheckAsync**

Follow the same pattern as `ConnectorBackgroundService.SyncAllTenantsAsync()`:

1. Add `using Nocturne.Core.Contracts.Multitenancy;` and `using Microsoft.EntityFrameworkCore;` and `using Nocturne.Infrastructure.Data;`
2. Rename `PerformHealthCheckAsync` to `PerformHealthCheckForTenantAsync` and make it accept a tenant-scoped `IServiceProvider` parameter instead of creating its own scope.
3. Add a new `PerformHealthCheckAsync` that iterates all active tenants:

```csharp
private async Task PerformHealthCheckAsync(CancellationToken cancellationToken)
{
    // Lookup active tenants using unfiltered context
    using var lookupScope = _serviceProvider.CreateScope();
    var factory = lookupScope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
    await using var lookupContext = await factory.CreateDbContextAsync(cancellationToken);
    var tenants = await lookupContext.Tenants.AsNoTracking()
        .Where(t => t.IsActive)
        .Select(t => new { t.Id, t.Slug, t.DisplayName })
        .ToListAsync(cancellationToken);

    foreach (var tenant in tenants)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
            tenantAccessor.SetTenant(new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, true));

            await PerformHealthCheckForTenantAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during device health monitoring for tenant {TenantSlug}", tenant.Slug);
        }
    }
}
```

4. Update `PerformHealthCheckForTenantAsync` to resolve services from the passed-in `IServiceProvider` instead of creating its own scope.
5. The `EvaluateSensorWarmupSuggestionAsync` method also creates its own scope (line 365) — it should use the tenant-scoped scope already established. Pass the scoped `IServiceProvider` through or resolve `ITrackerSuggestionService` at the top of `PerformHealthCheckForTenantAsync`.

**Step 2: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 3: Commit**

```bash
git add src/API/Nocturne.API/Services/BackgroundServices/DeviceHealthMonitoringService.cs
git commit -m "fix: add per-tenant iteration to DeviceHealthMonitoringService

Previously created a single scope without tenant context, causing
queries with Guid.Empty to return no devices."
```

---

## Task 4: Add per-tenant iteration to CompressionLowDetectionService

Same problem as DeviceHealthMonitoringService. Resolves services without setting tenant context.

**Files:**
- Modify: `src/API/Nocturne.API/Services/BackgroundServices/CompressionLowDetectionService.cs`

**Step 1: Add tenant iteration to ExecuteAsync and DetectForNightAsync**

1. Add tenant-related imports.
2. The `ExecuteAsync` loop currently creates a scope to read settings, calculates delay, then calls `DetectForNightAsync`. This needs to iterate per-tenant. The approach:
   - Create a `RunForAllTenantsAsync` method that looks up all active tenants (same pattern as Task 3), creates a per-tenant scope, sets the tenant context, then calls the detection logic.
   - The delay/scheduling logic in `ExecuteAsync` should remain but the actual detection should be per-tenant.
3. `DetectForNightAsync` (line 92) creates its own scope at line 96. It's also a `public` method (part of `ICompressionLowDetectionService`). For the public interface method, it should work within the current scope's tenant context. For the background service loop, wrap it in per-tenant iteration.

Best approach: in `ExecuteAsync`, after calculating the delay and waking up, iterate all tenants and call `DetectForNightAsync` within each tenant scope. `DetectForNightAsync` already creates its own scope — modify it to accept an optional `IServiceProvider` parameter, or have the per-tenant wrapper set the tenant accessor before calling it.

Since `DetectForNightAsync` creates its own scope via `_serviceProvider.CreateScope()`, and the `_serviceProvider` is the root provider, the new scope won't inherit the tenant context. The simplest fix: make the per-tenant loop set the tenant, then when `DetectForNightAsync` creates its scope, also set the tenant on that scope. Or refactor `DetectForNightAsync` to accept a pre-built scope.

Recommended: Extract an internal `DetectForNightInternalAsync(DateOnly nightOf, IServiceProvider scopeProvider, CancellationToken)` that accepts the scoped provider. The public `DetectForNightAsync` creates its own scope (and inherits the ambient tenant from `ITenantAccessor` since it's scoped), while the background loop calls `DetectForNightInternalAsync` with the tenant-scoped provider.

**Step 2: Fix the hardcoded `userId: "default"` at line 404**

Change `userId: "default"` to use the resolved subject/user ID from the tenant context. Since this is a background service without a user, use the tenant ID as a string:

```csharp
userId: tenantAccessor.TenantId.ToString(),
```

Or if there's a convention for system-generated notifications, follow that. The `InAppNotificationEntity.UserId` is a string field — use the tenant ID to scope it.

**Step 3: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 4: Commit**

```bash
git add src/API/Nocturne.API/Services/BackgroundServices/CompressionLowDetectionService.cs
git commit -m "fix: add per-tenant iteration to CompressionLowDetectionService

Previously resolved services without tenant context. Also replaced
hardcoded userId 'default' with the tenant ID."
```

---

## Task 5: Add per-tenant iteration to NotificationResolutionService

Same pattern. This service queries `InAppNotificationEntity` (which is `ITenantScoped`) without tenant context.

**Files:**
- Modify: `src/API/Nocturne.API/Services/NotificationResolutionService.cs`

**Step 1: Add tenant iteration**

1. Add tenant-related imports.
2. `EvaluatePendingNotificationsAsync` at line 70 creates a scope via `_scopeFactory.CreateScope()` without setting tenant context. Refactor to:
   - Look up all active tenants.
   - For each tenant, create a scope, set tenant context, then evaluate that tenant's pending notifications.

The pattern is the same as Tasks 3 and 4. Use `IServiceProvider` (inject it alongside `IServiceScopeFactory`, or switch to just `IServiceProvider`).

**Step 2: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 3: Commit**

```bash
git add src/API/Nocturne.API/Services/NotificationResolutionService.cs
git commit -m "fix: add per-tenant iteration to NotificationResolutionService

Previously queried InAppNotificationEntity without tenant context,
causing notifications to be silently skipped."
```

---

## Task 6: Invalidate tenant cache on update/deactivation

When an admin updates or deactivates a tenant, the 5-minute cache in `TenantResolutionMiddleware` continues serving the old state.

**Files:**
- Modify: `src/API/Nocturne.API/Services/TenantService.cs`

**Step 1: Inject IMemoryCache into TenantService**

Add `IMemoryCache` to the constructor:

```csharp
private readonly IMemoryCache _cache;

public TenantService(IDbContextFactory<NocturneDbContext> factory, IMemoryCache cache)
{
    _factory = factory;
    _cache = cache;
}
```

**Step 2: Invalidate cache after updates**

In `UpdateAsync`, after `SaveChangesAsync`, evict the cache:

```csharp
_cache.Remove($"tenant:{tenant.Slug}");
if (tenant.IsDefault)
    _cache.Remove("tenant:__default__");
```

You need to read the slug before update (it's already loaded via `FindAsync`). Add this after the `SaveChangesAsync` call.

**Step 3: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 4: Commit**

```bash
git add src/API/Nocturne.API/Services/TenantService.cs
git commit -m "fix: invalidate tenant cache on update/deactivation

TenantResolutionMiddleware caches TenantContext for 5 minutes.
Without invalidation, deactivating a tenant had no effect until
the cache expired."
```

---

## Task 7: Add duplicate guard to TenantService.AddMemberAsync

A race condition on concurrent first requests causes an unhandled unique constraint violation, which `AuthenticationMiddleware` catches as a generic exception and returns unauthenticated.

**Files:**
- Modify: `src/API/Nocturne.API/Services/TenantService.cs`

**Step 1: Add duplicate check or catch DbUpdateException**

In `AddMemberAsync`, wrap the insert in a try/catch for `DbUpdateException`:

```csharp
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
```

**Step 2: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 3: Commit**

```bash
git add src/API/Nocturne.API/Services/TenantService.cs
git commit -m "fix: handle duplicate tenant membership gracefully

Concurrent first requests could trigger a unique constraint
violation in AddMemberAsync, causing AuthenticationMiddleware
to return unauthenticated."
```

---

## Task 8: Standardize TenantRole constants

`TenantRole` defines `owner`, `caretaker`, `readonly`. But `UserSeedService` uses `"admin"` and `"member"`, and `AuthenticationMiddleware` auto-enrollment uses `"member"`. These need to be consistent.

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TenantMemberEntity.cs`
- Modify: `src/API/Nocturne.API/Services/Auth/UserSeedService.cs`
- Modify: `src/API/Nocturne.API/Middleware/AuthenticationMiddleware.cs`

**Step 1: Add `Member` role to TenantRole constants**

The existing roles (`Owner`, `Caretaker`, `ReadOnly`) make sense. We need to add `Member` for standard users. The `"admin"` usage in `UserSeedService` should map to `Owner` (the seed user is the instance owner).

In `TenantMemberEntity.cs`, add to `TenantRole`:

```csharp
public static class TenantRole
{
    public const string Owner = "owner";
    public const string Member = "member";
    public const string Caretaker = "caretaker";
    public const string ReadOnly = "readonly";
}
```

**Step 2: Update UserSeedService**

At line 196, change:
```csharp
var role = userConfig.IsAdmin ? "admin" : "member";
```
To:
```csharp
var role = userConfig.IsAdmin ? TenantRole.Owner : TenantRole.Member;
```

Add `using Nocturne.Infrastructure.Data.Entities;` if not already present.

**Step 3: Update AuthenticationMiddleware**

At line 123, change:
```csharp
"member");
```
To:
```csharp
TenantRole.Member);
```

Add `using Nocturne.Infrastructure.Data.Entities;` if not already present.

**Step 4: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 5: Commit**

```bash
git add src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TenantMemberEntity.cs src/API/Nocturne.API/Services/Auth/UserSeedService.cs src/API/Nocturne.API/Middleware/AuthenticationMiddleware.cs
git commit -m "fix: standardize tenant role strings to use TenantRole constants

UserSeedService used 'admin'/'member' and AuthenticationMiddleware
used 'member' as raw strings. Now all use TenantRole constants."
```

---

## Task 9: Make SystemEventEntity tenant-scoped

System events (pump alarms, CGM connectivity events, etc.) are per-user/per-device data that must be scoped to a tenant.

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/SystemEventEntity.cs`
- Create: new EF migration

**Step 1: Add ITenantScoped to SystemEventEntity (as nullable first)**

Add `using Nocturne.Infrastructure.Data.Multitenancy;` (or wherever `ITenantScoped` lives — check existing entities for the correct import).

First, add `TenantId` as **nullable** so the migration can be applied to existing rows:

```csharp
[Table("system_events")]
public class SystemEventEntity : ITenantScoped
{
    // ... existing fields ...

    [Column("tenant_id")]
    public Guid TenantId { get; set; }
}
```

Note: Even though the property is non-nullable `Guid`, EF will generate an `AddColumn` with a default of `Guid.Empty`. To handle existing data properly, we'll use a two-step migration approach — see below.

Check how other entities implement `ITenantScoped` to match the exact pattern (look at any existing `ITenantScoped` entity for the import and property style).

**Step 2: Generate EF migration**

Stop Aspire first, then:

```bash
dotnet build src/Infrastructure/Nocturne.Infrastructure.Data -p:GenerateNSwagClient=false
dotnet ef migrations add AddTenantIdToSystemEvents --project src/Infrastructure/Nocturne.Infrastructure.Data --startup-project src/API/Nocturne.API --no-build
```

**Step 3: Review the generated migration**

EF will scaffold the `AddColumn` and FK automatically. Review the generated migration file — it should add the `tenant_id` column with a default value and create the FK to `tenants`. If EF generates it as non-nullable with a default of `Guid.Empty`, that's fine for dev (there are unlikely to be existing rows). If it does need backfill, add a single `migrationBuilder.Sql("UPDATE system_events SET tenant_id = (SELECT id FROM tenants WHERE is_default = true) WHERE tenant_id = '00000000-0000-0000-0000-000000000000'");` before the FK creation. Do NOT write RLS policies or raw SQL beyond this — the existing `EnforceMultitenancy` migration already set up the RLS infrastructure.

**Step 4: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 5: Commit**

```bash
git add src/Infrastructure/Nocturne.Infrastructure.Data/Entities/SystemEventEntity.cs src/Infrastructure/Nocturne.Infrastructure.Data/Migrations/
git commit -m "feat: make SystemEventEntity tenant-scoped

System events (pump alarms, CGM connectivity) are per-device data
that must be scoped to a tenant. Added ITenantScoped, tenant_id
column, and migration to backfill existing data."
```

---

## Task 10: Remove auto-enrollment from AuthenticationMiddleware

The current auto-enrollment at `AuthenticationMiddleware.cs` lines 114-134 blindly adds any authenticated user to whatever tenant the subdomain resolves to. In self-hosted single-tenant mode this is harmless, but in multi-tenant mode it's a security hole. Replace it with a 403.

**Files:**
- Modify: `src/API/Nocturne.API/Middleware/AuthenticationMiddleware.cs`

**Step 1: Replace auto-enrollment with 403**

At lines 114-135, replace the auto-enrollment block:

```csharp
if (!isMember)
{
    // Auto-enroll authenticated users into the tenant they're accessing
    var tenantService = context.RequestServices.GetRequiredService<ITenantService>();
    try
    {
        await tenantService.AddMemberAsync(
            resolvedAuth.TenantId!.Value,
            resolvedAuth.SubjectId!.Value,
            "member");
        // ...
    }
    // ...
}
```

With:

```csharp
if (!isMember)
{
    _logger.LogWarning(
        "Subject {SubjectId} is not a member of tenant {TenantId}",
        resolvedAuth.SubjectId, resolvedAuth.TenantId);
    SetUnauthenticated(context);
}
```

**Step 2: Ensure registration adds tenant membership**

Read `src/API/Nocturne.API/Controllers/LocalAuthController.cs` to understand the registration flow. After `_identityService.RegisterAsync` succeeds, add the new user to the current tenant (resolved from `TenantContext` in `HttpContext.Items`):

```csharp
// After successful registration, add user to current tenant
if (context.Items["TenantContext"] is TenantContext tenantCtx)
{
    var tenantService = context.RequestServices.GetRequiredService<ITenantService>();
    await tenantService.AddMemberAsync(tenantCtx.TenantId, result.User!.Id, TenantRole.Owner);
}
```

**Design decision (confirmed):** For self-hosted mode (no `BaseDomain` configured), the first registered user on a tenant gets `TenantRole.Owner`, subsequent users get `TenantRole.Member`. For SaaS mode (BaseDomain is set), registration adds the user to the resolved tenant as `TenantRole.Member`.

Implementation: check if the tenant's current member count is 0 (first user) to assign `Owner`, otherwise assign `Member`. Use `ITenantMemberService` or query tenant members to determine this.

The `LocalAuthController` will need `ITenantService` and `ITenantAccessor` (or read from `HttpContext.Items["TenantContext"]`) injected.

**Step 3: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 4: Commit**

```bash
git add src/API/Nocturne.API/Middleware/AuthenticationMiddleware.cs src/API/Nocturne.API/Controllers/LocalAuthController.cs
git commit -m "fix: replace auto-enrollment with explicit tenant membership on registration

Auto-enrollment in AuthenticationMiddleware was a security risk in
multi-tenant mode. Now registration explicitly adds the user to
the current tenant, and non-members get a 403."
```

---

## Task 11: Replace hardcoded DefaultUserId in AlertOrchestrator and InProcessConnectorPublisher

These services use hardcoded user IDs. They should use the tenant context or accept a user ID parameter.

**Files:**
- Modify: `src/API/Nocturne.API/Services/Alerts/AlertOrchestrator.cs`
- Modify: `src/API/Nocturne.API/Services/ConnectorPublishing/InProcessConnectorPublisher.cs`

**Step 1: Fix AlertOrchestrator**

The `AlertOrchestrator` already receives `userId` as a parameter in `EvaluateAndProcessSensorGlucoseAsync`. The `DefaultUserId` is only used as a fallback when `userId` is null. Since alerts are tenant-scoped via the DB filter, and the caller should always pass a user ID, change the fallback to use the tenant ID:

1. Add `ITenantAccessor` to the primary constructor.
2. Replace the `DefaultUserId` constant with: `_tenantAccessor.TenantId.ToString()`

**Step 2: Fix InProcessConnectorPublisher**

The `DefaultUserId = "default"` is used for food entry creation and other places. This class is resolved within a tenant-scoped `IServiceScope` by `ConnectorBackgroundService`. Inject `ITenantAccessor` and use the tenant ID as the user identifier.

**Step 3: Build and test**

```bash
dotnet build src/API/Nocturne.API
dotnet test --filter "Category!=Integration&Category!=Performance"
```

**Step 4: Commit**

```bash
git add src/API/Nocturne.API/Services/Alerts/AlertOrchestrator.cs src/API/Nocturne.API/Services/ConnectorPublishing/InProcessConnectorPublisher.cs
git commit -m "fix: replace hardcoded DefaultUserId with tenant-aware user ID

AlertOrchestrator and InProcessConnectorPublisher used hardcoded
placeholder user IDs. Now use the resolved tenant ID as fallback."
```

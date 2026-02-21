# Connector Health Tracking Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Surface connector errors and health state to users so they can see when connectors fail and why

**Architecture:** Extend `connector_configurations` table with health tracking columns. Update `ConnectorBackgroundService` to persist health state on each sync cycle. Update `ConnectorHealthService` to read and expose health data. Update frontend to display error messages and timestamps.

**Tech Stack:** .NET 10, EF Core, PostgreSQL, SvelteKit, NSwag

---

## Task 1: Add Health Tracking Fields to Database

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/ConnectorConfigurationEntity.cs`
- Create: `src/Infrastructure/Nocturne.Infrastructure.Data/Migrations/YYYYMMDDHHMMSS_AddConnectorHealthTracking.cs` (will be auto-generated)

**Step 1: Add health properties to ConnectorConfigurationEntity**

Add the following properties after the existing properties in `ConnectorConfigurationEntity.cs`:

```csharp
/// <summary>
/// When the connector last attempted to sync
/// </summary>
[Column("last_sync_attempt")]
public DateTime? LastSyncAttempt { get; set; }

/// <summary>
/// When the connector last successfully completed a sync
/// </summary>
[Column("last_successful_sync")]
public DateTime? LastSuccessfulSync { get; set; }

/// <summary>
/// The error message from the most recent failure
/// </summary>
[Column("last_error_message")]
public string? LastErrorMessage { get; set; }

/// <summary>
/// When the error occurred
/// </summary>
[Column("last_error_at")]
public DateTime? LastErrorAt { get; set; }

/// <summary>
/// Current health status
/// </summary>
[Column("is_healthy")]
public bool IsHealthy { get; set; } = true;
```

**Step 2: Create EF migration**

Run: `dotnet ef migrations add AddConnectorHealthTracking --project src/Infrastructure/Nocturne.Infrastructure.Data --startup-project src/API/Nocturne.API -p:GenerateNSwagClient=false`

Expected: Migration created successfully

**Step 3: Review generated migration**

Check that the generated migration includes all five columns with correct types:
- `last_sync_attempt` (timestamp with time zone, nullable)
- `last_successful_sync` (timestamp with time zone, nullable)
- `last_error_message` (text, nullable)
- `last_error_at` (timestamp with time zone, nullable)
- `is_healthy` (boolean, not null, default true)

**Step 4: Commit database changes**

```bash
git add src/Infrastructure/Nocturne.Infrastructure.Data/Entities/ConnectorConfigurationEntity.cs
git add src/Infrastructure/Nocturne.Infrastructure.Data/Migrations/
git commit -m "feat: add health tracking fields to connector configurations"
```

---

## Task 2: Add HealthState DTO and Service Interface Methods

**Files:**
- Create: `src/Core/Nocturne.Core.Models/Configuration/ConnectorHealthStateDto.cs`
- Modify: `src/Core/Nocturne.Core.Contracts/IConnectorConfigurationService.cs`

**Step 1: Create ConnectorHealthStateDto**

Create new file `src/Core/Nocturne.Core.Models/Configuration/ConnectorHealthStateDto.cs`:

```csharp
namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Health state information for a connector
/// </summary>
public class ConnectorHealthStateDto
{
    public DateTime? LastSyncAttempt { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public bool IsHealthy { get; set; }
}
```

**Step 2: Add health state methods to IConnectorConfigurationService**

Add these method signatures to `IConnectorConfigurationService.cs` (after existing methods):

```csharp
/// <summary>
/// Gets the health state for a connector
/// </summary>
Task<ConnectorHealthStateDto?> GetHealthStateAsync(
    string connectorName,
    CancellationToken cancellationToken = default
);

/// <summary>
/// Updates the health state for a connector
/// </summary>
Task UpdateHealthStateAsync(
    string connectorName,
    DateTime? lastSyncAttempt = null,
    DateTime? lastSuccessfulSync = null,
    string? lastErrorMessage = null,
    DateTime? lastErrorAt = null,
    bool? isHealthy = null,
    CancellationToken cancellationToken = default
);
```

**Step 3: Commit DTO and interface changes**

```bash
git add src/Core/Nocturne.Core.Models/Configuration/ConnectorHealthStateDto.cs
git add src/Core/Nocturne.Core.Contracts/IConnectorConfigurationService.cs
git commit -m "feat: add health state DTO and service methods"
```

---

## Task 3: Implement Health State Methods in ConnectorConfigurationService

**Files:**
- Modify: `src/API/Nocturne.API/Services/ConnectorConfigurationService.cs`

**Step 1: Implement GetHealthStateAsync**

Add this method to `ConnectorConfigurationService`:

```csharp
public async Task<ConnectorHealthStateDto?> GetHealthStateAsync(
    string connectorName,
    CancellationToken cancellationToken = default
)
{
    var config = await _dbContext.ConnectorConfigurations
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, cancellationToken);

    if (config == null)
        return null;

    return new ConnectorHealthStateDto
    {
        LastSyncAttempt = config.LastSyncAttempt,
        LastSuccessfulSync = config.LastSuccessfulSync,
        LastErrorMessage = config.LastErrorMessage,
        LastErrorAt = config.LastErrorAt,
        IsHealthy = config.IsHealthy
    };
}
```

**Step 2: Implement UpdateHealthStateAsync**

Add this method to `ConnectorConfigurationService`:

```csharp
public async Task UpdateHealthStateAsync(
    string connectorName,
    DateTime? lastSyncAttempt = null,
    DateTime? lastSuccessfulSync = null,
    string? lastErrorMessage = null,
    DateTime? lastErrorAt = null,
    bool? isHealthy = null,
    CancellationToken cancellationToken = default
)
{
    var config = await _dbContext.ConnectorConfigurations
        .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, cancellationToken);

    if (config == null)
    {
        _logger.LogWarning(
            "Cannot update health state for connector {ConnectorName}: configuration not found",
            connectorName
        );
        return;
    }

    // Only update fields that were provided
    if (lastSyncAttempt.HasValue)
        config.LastSyncAttempt = lastSyncAttempt.Value;

    if (lastSuccessfulSync.HasValue)
        config.LastSuccessfulSync = lastSuccessfulSync.Value;

    if (lastErrorMessage != null)
        config.LastErrorMessage = lastErrorMessage;
    else if (lastErrorMessage == string.Empty)
        config.LastErrorMessage = null; // Explicit clear

    if (lastErrorAt.HasValue)
        config.LastErrorAt = lastErrorAt.Value;
    else if (lastErrorAt == DateTime.MinValue)
        config.LastErrorAt = null; // Explicit clear

    if (isHealthy.HasValue)
        config.IsHealthy = isHealthy.Value;

    config.SysUpdatedAt = DateTime.UtcNow;

    await _dbContext.SaveChangesAsync(cancellationToken);

    _logger.LogDebug(
        "Updated health state for connector {ConnectorName}: IsHealthy={IsHealthy}",
        connectorName,
        config.IsHealthy
    );
}
```

**Step 3: Commit service implementation**

```bash
git add src/API/Nocturne.API/Services/ConnectorConfigurationService.cs
git commit -m "feat: implement health state read/write methods"
```

---

## Task 4: Update ConnectorStatusDto

**Files:**
- Modify: `src/API/Nocturne.API/Models/ConnectorStatusDto.cs`

**Step 1: Add health fields to ConnectorStatusDto**

Add these properties after the existing properties:

```csharp
/// <summary>
/// When the connector last attempted to sync
/// </summary>
public DateTime? LastSyncAttempt { get; set; }

/// <summary>
/// When the connector last successfully completed a sync
/// </summary>
public DateTime? LastSuccessfulSync { get; set; }

/// <summary>
/// When the last error occurred
/// </summary>
public DateTime? LastErrorAt { get; set; }
```

Note: `StateMessage` already exists and will be used for error messages.

**Step 2: Rebuild to regenerate NSwag client**

Run: `aspire run` (this will rebuild and regenerate the NSwag client)

Wait for Aspire to start, then stop it (Ctrl+C).

**Step 3: Commit DTO changes**

```bash
git add src/API/Nocturne.API/Models/ConnectorStatusDto.cs
git add src/Web/packages/app/src/lib/api/
git commit -m "feat: add health timestamps to ConnectorStatusDto"
```

---

## Task 5: Update ConnectorHealthService to Read Health State

**Files:**
- Modify: `src/API/Nocturne.API/Services/ConnectorHealthService.cs`

**Step 1: Update GetConnectorStatusWithDbStatsAsync to read health state**

In the `GetConnectorStatusWithDbStatsAsync` method, after getting `enabledConfig` and before the "If explicitly disabled" check, add:

```csharp
// Get health state for the connector
var healthState = await connectorConfigService.GetHealthStateAsync(
    connector.Id,
    cancellationToken
);
```

**Step 2: Update disabled connector return to include health state**

In the "If explicitly disabled" block (around line 91-107), update the return statement:

```csharp
return new ConnectorStatusDto
{
    Id = connector.Id,
    Name = connector.Id,
    Status = "Disabled",
    TotalEntries = dbStats.TotalItems,
    LastEntryTime = dbStats.LastItemTime,
    EntriesLast24Hours = dbStats.ItemsLast24Hours,
    State = "Disabled",
    IsHealthy = false,
    StateMessage = healthState?.LastErrorMessage,
    LastSyncAttempt = healthState?.LastSyncAttempt,
    LastSuccessfulSync = healthState?.LastSuccessfulSync,
    LastErrorAt = healthState?.LastErrorAt,
    TotalItemsBreakdown = totalBreakdown.Count > 0 ? totalBreakdown : null,
    ItemsLast24HoursBreakdown = last24HBreakdown.Count > 0 ? last24HBreakdown : null,
};
```

**Step 3: Update running connector return to include health state**

Update the `liveStatus` variable (around line 110-124):

```csharp
var liveStatus = new ConnectorStatusDto
{
    Id = connector.Id,
    Name = connector.Id,
    Status = enabledConfig == true ? "Running" : "Not Configured",
    IsHealthy = healthState?.IsHealthy ?? (enabledConfig == true),
    State = enabledConfig == true ? "Running" : "Not Configured",
    StateMessage = healthState?.LastErrorMessage,
    LastSyncAttempt = healthState?.LastSyncAttempt,
    LastSuccessfulSync = healthState?.LastSuccessfulSync,
    LastErrorAt = healthState?.LastErrorAt,
    TotalEntries = dbStats.TotalItems,
    LastEntryTime = dbStats.LastItemTime,
    EntriesLast24Hours = dbStats.ItemsLast24Hours,
    TotalItemsBreakdown = totalBreakdown.Count > 0 ? totalBreakdown : null,
    ItemsLast24HoursBreakdown = last24HBreakdown.Count > 0 ? last24HBreakdown : null,
};
```

**Step 4: Commit health service changes**

```bash
git add src/API/Nocturne.API/Services/ConnectorHealthService.cs
git commit -m "feat: read and expose connector health state"
```

---

## Task 6: Update ConnectorBackgroundService to Track Health

**Files:**
- Modify: `src/API/Nocturne.API/Services/BackgroundServices/ConnectorBackgroundService.cs`

**Step 1: Add UpdateHealthStateAsync helper method**

Add this method to the `ConnectorBackgroundService<TConfig>` class (before `ExecuteAsync`):

```csharp
/// <summary>
/// Updates the health state for this connector in the database
/// </summary>
protected async Task UpdateHealthStateAsync(
    DateTime? lastSyncAttempt = null,
    DateTime? lastSuccessfulSync = null,
    string? lastErrorMessage = null,
    DateTime? lastErrorAt = null,
    bool? isHealthy = null,
    CancellationToken cancellationToken = default
)
{
    try
    {
        using var scope = ServiceProvider.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<IConnectorConfigurationService>();

        await configService.UpdateHealthStateAsync(
            ConnectorName,
            lastSyncAttempt,
            lastSuccessfulSync,
            lastErrorMessage,
            lastErrorAt,
            isHealthy,
            cancellationToken
        );
    }
    catch (Exception ex)
    {
        Logger.LogWarning(
            ex,
            "Failed to update health state for {ConnectorName}",
            ConnectorName
        );
    }
}
```

**Step 2: Track sync attempts in ExecuteAsync**

In the `ExecuteAsync` method, inside the `do` loop, before calling `PerformSyncAsync`, add:

```csharp
// Record sync attempt
await UpdateHealthStateAsync(
    lastSyncAttempt: DateTime.UtcNow,
    stoppingToken
);
```

**Step 3: Track sync results**

Update the success/failure handling (around line 177-189):

```csharp
var success = await PerformSyncAsync(stoppingToken);

if (success)
{
    Logger.LogInformation(
        "{ConnectorName} data sync completed successfully",
        ConnectorName
    );

    // Clear error state, mark as healthy
    await UpdateHealthStateAsync(
        lastSuccessfulSync: DateTime.UtcNow,
        isHealthy: true,
        lastErrorMessage: string.Empty, // Explicit clear
        lastErrorAt: DateTime.MinValue, // Explicit clear
        stoppingToken
    );
}
else
{
    Logger.LogWarning("{ConnectorName} data sync failed", ConnectorName);

    // Mark as unhealthy with generic error
    await UpdateHealthStateAsync(
        isHealthy: false,
        lastErrorMessage: "Sync failed after retries",
        lastErrorAt: DateTime.UtcNow,
        stoppingToken
    );
}
```

**Step 4: Track exceptions**

Update the exception handler (around line 191-194):

```csharp
catch (Exception ex)
{
    Logger.LogError(ex, "Error during {ConnectorName} data sync cycle", ConnectorName);

    // Record exception in health state
    await UpdateHealthStateAsync(
        isHealthy: false,
        lastErrorMessage: ex.Message,
        lastErrorAt: DateTime.UtcNow,
        stoppingToken
    );
}
```

**Step 5: Commit background service changes**

```bash
git add src/API/Nocturne.API/Services/BackgroundServices/ConnectorBackgroundService.cs
git commit -m "feat: track connector health state in background service"
```

---

## Task 7: Update Frontend to Display Error Information

**Files:**
- Modify: `src/Web/packages/app/src/routes/settings/connectors/+page.svelte`

**Step 1: Add timestamp formatting helper**

Add this helper function near the other helper functions (around line 730):

```typescript
function formatRelativeTime(date: string | Date | undefined | null): string {
  if (!date) return "Never";

  const d = typeof date === "string" ? new Date(date) : date;
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return "Just now";
  if (diffMins < 60) return `${diffMins} minute${diffMins !== 1 ? "s" : ""} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours !== 1 ? "s" : ""} ago`;
  if (diffDays < 7) return `${diffDays} day${diffDays !== 1 ? "s" : ""} ago`;

  return d.toLocaleDateString();
}
```

**Step 2: Update connected connector display to show error state**

Find the "Connected connector" section (around line 1214-1260) and update the button styling to be conditional:

Change line 1218 from:
```svelte
class="flex w-full items-center gap-4 p-4 rounded-lg border hover:border-primary/50 hover:bg-accent/50 transition-colors text-left group border-green-300 dark:border-green-700 bg-green-50/50 dark:bg-green-950/20"
```

To:
```svelte
class="flex w-full items-center gap-4 p-4 rounded-lg border hover:border-primary/50 hover:bg-accent/50 transition-colors text-left group {connectorStatus.isHealthy
  ? 'border-green-300 dark:border-green-700 bg-green-50/50 dark:bg-green-950/20'
  : 'border-red-300 dark:border-red-700 bg-red-50/50 dark:bg-red-950/20'}"
```

**Step 3: Update icon styling to be conditional**

Change line 1242 from:
```svelte
class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900/30"
```

To:
```svelte
class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg {connectorStatus.isHealthy
  ? 'bg-green-100 dark:bg-green-900/30'
  : 'bg-red-100 dark:bg-red-900/30'}"
```

And update the Icon color on line 1244 from:
```svelte
<Icon class="h-5 w-5 text-green-600 dark:text-green-400" />
```

To:
```svelte
<Icon class="h-5 w-5 {connectorStatus.isHealthy
  ? 'text-green-600 dark:text-green-400'
  : 'text-red-600 dark:text-red-400'}" />
```

**Step 4: Add error message display**

After the connector name and status badges (find line 1249 where the Syncing badge is), add this error display block:

```svelte
{#if !connectorStatus.isHealthy && connectorStatus.stateMessage}
  <div class="w-full mt-2 text-destructive text-sm flex items-start gap-2">
    <AlertCircle class="h-4 w-4 mt-0.5 shrink-0" />
    <div class="flex-1 min-w-0">
      <div class="font-medium">{connectorStatus.stateMessage}</div>
      <div class="text-muted-foreground text-xs mt-1">
        {#if connectorStatus.lastSyncAttempt}
          Last attempted: {formatRelativeTime(connectorStatus.lastSyncAttempt)}
        {/if}
        {#if connectorStatus.lastSuccessfulSync}
          {#if connectorStatus.lastSyncAttempt}•{/if}
          Last successful: {formatRelativeTime(connectorStatus.lastSuccessfulSync)}
        {/if}
      </div>
    </div>
  </div>
{/if}
```

**Step 5: Commit frontend changes**

```bash
git add src/Web/packages/app/src/routes/settings/connectors/+page.svelte
git commit -m "feat: display connector error state and timestamps"
```

---

## Task 8: Run Migration and Manual Test

**Files:**
- N/A (testing only)

**Step 1: Run the database migration**

Run: `aspire run`

Wait for the application to start. The migration should run automatically.

Check Aspire dashboard logs for:
```
Applying migration '..._AddConnectorHealthTracking'
```

**Step 2: Verify connectors page loads**

Navigate to: `https://localhost:1612/settings/connectors`

Expected: Page loads without errors, connectors show without error messages (since they haven't synced yet)

**Step 3: Wait for a connector to sync**

Wait for a connector sync cycle (check connector sync interval in settings, typically 5-15 minutes).

Expected: Connector updates with health state

**Step 4: Test error state (optional)**

To test error display:
1. Go to connector configuration
2. Change credentials to intentionally invalid values
3. Wait for next sync cycle
4. Verify error message appears with timestamps

**Step 5: Verify error clears on success**

Fix the credentials and wait for next sync.

Expected: Error message clears, connector shows as healthy

**Step 6: Final commit**

```bash
git add -A
git commit -m "test: verify connector health tracking integration"
```

---

## Success Criteria

- [ ] Database migration runs successfully
- [ ] Connectors track last sync attempt timestamp
- [ ] Successful syncs clear error state
- [ ] Failed syncs (after retries) show error message
- [ ] Frontend displays error messages with timestamps
- [ ] Error state clears when connector recovers
- [ ] Disabled connectors preserve health state
- [ ] Manual testing with invalid credentials shows errors

# Connector Health Tracking Design

**Date:** 2026-02-21
**Status:** Approved

## Problem

Connectors currently log errors when they fail (e.g., authentication errors, wrong server configuration), but these errors are not persisted or surfaced to the frontend. Users cannot see when their connectors are failing or why. For example, if Dexcom credentials are incorrect or pointing to the wrong server, the backend logs the error but the frontend continues to show the connector as "Running".

## Requirements

1. **Full error details**: Display error message, last attempt timestamp, and last successful sync timestamp
2. **Current state only**: Track only the current error state; clear errors when connector succeeds
3. **No categorization**: Single error type with no distinction between auth/network/config errors
4. **Show errors after retries**: Only display errors after all retry attempts are exhausted (not on first failure)

## Approach

**Selected: Extend ConnectorConfigurationEntity**

Add health tracking fields directly to the existing `connector_configurations` table. This is the simplest approach for tracking current state without requiring a separate table or caching layer.

### Alternatives Considered

- **Separate ConnectorHealthEntity**: Clean separation but adds JOIN overhead
- **In-memory cache with DB backup**: Fast but complex and can lose state on restart

## Design

### 1. Database Schema Changes

Add the following columns to `connector_configurations` table:

```sql
ALTER TABLE connector_configurations ADD COLUMN last_sync_attempt timestamptz NULL;
ALTER TABLE connector_configurations ADD COLUMN last_successful_sync timestamptz NULL;
ALTER TABLE connector_configurations ADD COLUMN last_error_message text NULL;
ALTER TABLE connector_configurations ADD COLUMN last_error_at timestamptz NULL;
ALTER TABLE connector_configurations ADD COLUMN is_healthy boolean NOT NULL DEFAULT true;
```

**Update patterns:**
- **On sync attempt**: Set `last_sync_attempt = NOW()`
- **On success**: Set `last_successful_sync = NOW()`, clear error fields, set `is_healthy = true`
- **On failure** (after retries): Set `last_error_message`, `last_error_at = NOW()`, set `is_healthy = false`

### 2. Backend Changes

#### ConnectorBackgroundService

Update `ExecuteAsync` method to track health state:

```csharp
// Before sync
await UpdateHealthStateAsync(lastSyncAttempt: DateTime.UtcNow);

// After sync
if (success)
{
    await UpdateHealthStateAsync(
        lastSuccessfulSync: DateTime.UtcNow,
        isHealthy: true,
        lastErrorMessage: null,
        lastErrorAt: null
    );
}
else
{
    await UpdateHealthStateAsync(
        isHealthy: false,
        lastErrorMessage: "Sync failed after retries",
        lastErrorAt: DateTime.UtcNow
    );
}

// On exception
catch (Exception ex)
{
    await UpdateHealthStateAsync(
        isHealthy: false,
        lastErrorMessage: ex.Message,
        lastErrorAt: DateTime.UtcNow
    );
}
```

Add `UpdateHealthStateAsync` helper method that uses `IConnectorConfigurationService` to update the database.

#### ConnectorHealthService

Update `GetConnectorStatusWithDbStatsAsync` to read health fields from database:

```csharp
var healthState = await connectorConfigService.GetHealthStateAsync(connector.Id, cancellationToken);

return new ConnectorStatusDto
{
    // ... existing fields ...
    IsHealthy = healthState?.IsHealthy ?? (enabledConfig == true),
    StateMessage = healthState?.LastErrorMessage,
    LastSyncAttempt = healthState?.LastSyncAttempt,
    LastSuccessfulSync = healthState?.LastSuccessfulSync,
    LastErrorAt = healthState?.LastErrorAt
};
```

#### IConnectorConfigurationService

Add new methods:
- `GetHealthStateAsync(string connectorName, CancellationToken ct)`
- `UpdateHealthStateAsync(string connectorName, HealthStateUpdate update, CancellationToken ct)`

### 3. API Changes

Update `ConnectorStatusDto` to include new fields:

```csharp
public class ConnectorStatusDto
{
    // ... existing fields ...
    public DateTime? LastSyncAttempt { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastErrorAt { get; set; }
    // StateMessage already exists - will contain last_error_message
}
```

### 4. Frontend Changes

Update connector status display in `src/Web/packages/app/src/routes/settings/connectors/+page.svelte`:

**For unhealthy connectors:**
- Display error badge (red/destructive variant)
- Show error message from `stateMessage`
- Display timestamps for last attempt and last successful sync
- Include error timestamp

**UI pattern:**
```svelte
{#if !connectorStatus.isHealthy && connectorStatus.stateMessage}
  <div class="text-destructive text-sm flex items-center gap-2">
    <AlertCircle class="h-4 w-4" />
    <span>{connectorStatus.stateMessage}</span>
  </div>
  <div class="text-muted-foreground text-xs mt-1">
    Last attempted: {formatTimestamp(connectorStatus.lastSyncAttempt)}
    {#if connectorStatus.lastSuccessfulSync}
      • Last successful: {formatTimestamp(connectorStatus.lastSuccessfulSync)}
    {/if}
  </div>
{/if}
```

## Error Handling

- Errors are only recorded after all retry attempts are exhausted
- Transient failures during retries are not surfaced to users
- Error state is cleared on next successful sync
- If a connector is disabled, health state is preserved but not updated

## Testing Considerations

- Unit tests for `ConnectorBackgroundService` health state updates
- Integration tests for end-to-end health tracking flow
- Manual testing with intentionally incorrect credentials to verify error display
- Verify error clearing on successful sync after failure

## Migration Path

1. Create EF migration to add new columns
2. Run migration (columns default to NULL for existing records)
3. Deploy backend changes (backward compatible - handles NULL health state)
4. Deploy frontend changes (gracefully handles missing health data)
5. Connectors will populate health state on next sync cycle

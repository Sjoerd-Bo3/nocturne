# Alert Engine Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Replace the existing flat-threshold alert system with a composable condition-tree alert engine supporting escalation chains, excursion tracking, and multi-channel delivery dispatch.

**Architecture:** Clean Architecture layering. New entities in Infrastructure.Data, domain models and contracts in Core, evaluation engine and background services in API, REST endpoints in V4 controllers, setup wizard in the SvelteKit frontend. The old alert system is scrapped entirely — all old entities, services, repositories, models, and contracts are deleted and replaced.

**Tech Stack:** .NET 10, EF Core + PostgreSQL (JSONB for conditions), SignalR (fast-path delivery), xUnit + FluentAssertions + Moq (tests), SvelteKit 2 + Svelte 5 + shadcn-svelte (frontend).

**Reference:** [alert-engine-design.md](alert-engine-design.md) is the authoritative design document. All data models, state machines, and behavioural rules are defined there.

---

## Phase 0: Cleanup — Remove Old Alert System

### Task 0.1: Delete old alert entities, models, contracts, services, and repositories

This task removes everything from the old alert system. The new system is a clean break.

**Files to delete:**
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AlertRuleEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AlertHistoryEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/NotificationPreferencesEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/EmergencyContactEntity.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/AlertRuleRepository.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/AlertHistoryRepository.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/NotificationPreferencesRepository.cs`
- `src/Core/Nocturne.Core.Models/NotificationModels.cs` (the `AlertEvent`, `AlertType`, `AlertMonitoringOptions` etc — preserve the V1/V2 notification DTOs if they're needed for Nightscout compat, move them to a separate file if so)
- `src/Core/Nocturne.Core.Models/EscalationModels.cs`
- `src/Core/Nocturne.Core.Contracts/Alerts/IAlertOrchestrator.cs`
- `src/Core/Nocturne.Core.Contracts/Alerts/INotifier.cs`
- `src/Core/Nocturne.Core.Contracts/Alerts/INotifierDispatcher.cs`
- `src/API/Nocturne.API/Services/AlertProcessingService.cs`
- `src/API/Nocturne.API/Services/IAlertProcessingService.cs`
- `src/API/Nocturne.API/Services/AlertRulesEngine.cs`
- `src/API/Nocturne.API/Services/IAlertRulesEngine.cs`
- `src/API/Nocturne.API/Services/Alerts/AlertOrchestrator.cs`
- `src/API/Nocturne.API/Services/Alerts/NotifierDispatcher.cs`
- `src/API/Nocturne.API/Services/Alerts/Notifiers/SignalRNotifier.cs`
- `src/API/Nocturne.API/Services/Alerts/Notifiers/WebhookNotifier.cs`
- `src/API/Nocturne.API/Services/Alerts/Notifiers/PushoverNotifier.cs`

**Files to modify:**
- `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs` — Remove the 4 old DbSets (`AlertRules`, `AlertHistory`, `NotificationPreferences`, `EmergencyContacts`) and all their `ConfigureIndexes`/`ConfigureEntities` entries. Remove from `ConfigureTenantFilters` if explicitly listed.
- `src/API/Nocturne.API/Extensions/ServiceRegistrationExtensions.cs` — Gut the `AddAlertingAndMonitoring()` method body (keep the method shell — we'll fill it with new registrations later). Remove old `using` statements.
- `src/API/Nocturne.API/Services/ConnectorPublishing/GlucosePublisher.cs` — Remove `IAlertOrchestrator` dependency and the call to `EvaluateAndProcessSensorGlucoseAsync`. We'll re-add the new orchestrator dependency later.
- `src/API/Nocturne.API/Controllers/V1/EntriesController.cs` — Remove `IAlertOrchestrator` dependency and calls.
- `src/API/Nocturne.API/Controllers/V3/EntriesController.cs` — Same.
- `src/API/Nocturne.API/Controllers/V4/SensorGlucoseController.cs` — Same.
- Any test files referencing the old alert types — update or delete as needed.

**Steps:**
1. Delete all files listed above.
2. Fix all compile errors in modified files by removing references.
3. Run `dotnet build` on the solution — fix any remaining references.
4. Commit: `refactor: remove old alert system (AlertRuleEntity, AlertHistory, etc.)`

### Task 0.2: Create migration to drop old alert tables

**Steps:**
1. Generate migration: `dotnet ef migrations add DropOldAlertTables --project src/Infrastructure/Nocturne.Infrastructure.Data --startup-project src/API/Nocturne.API -p:GenerateNSwagClient=false`
2. Verify the migration drops `alert_rules`, `alert_history`, `notification_preferences`, `emergency_contacts` tables.
3. Commit: `migrate: drop old alert tables`

---

## Phase 1: Database Schema — New Alert Tables

### Task 1.1: Add `last_reading_at`, `timezone`, and `subject_name` to TenantEntity

**File:** `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TenantEntity.cs`

Add three new columns:

```csharp
/// <summary>
/// Timestamp of the most recent glucose reading for this tenant.
/// Updated on every glucose ingest. Used by signal loss detection.
/// </summary>
[Column("last_reading_at")]
public DateTime? LastReadingAt { get; set; }

/// <summary>
/// IANA timezone for this tenant (e.g. "America/New_York").
/// Used for schedule evaluation and display.
/// </summary>
[Column("timezone")]
[MaxLength(64)]
public string Timezone { get; set; } = "UTC";

/// <summary>
/// Preferred name for the person being monitored (e.g. "Alex").
/// Used in alert payloads. Falls back to DisplayName if null.
/// </summary>
[Column("subject_name")]
[MaxLength(128)]
public string? SubjectName { get; set; }
```

**Steps:**
1. Add the properties to `TenantEntity.cs`.
2. Update `GlucosePublisher` (and any other ingest path) to set `LastReadingAt` on the tenant after successful glucose ingest. This requires loading the tenant entity and updating it — or a raw SQL update for performance: `UPDATE tenants SET last_reading_at = @now WHERE id = @tenantId`.
3. Generate migration: `dotnet ef migrations add AddTenantAlertFields ...`
4. `dotnet build` — verify.
5. Commit: `feat: add last_reading_at, timezone, subject_name to tenants`

### Task 1.2: Create new alert entity classes

Create the following entity files in `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/`:

**File: `AlertRuleEntity.cs`** (replaces old one)

```csharp
[Table("alert_rules")]
public class AlertRuleEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [Column("description")]
    [MaxLength(512)]
    public string? Description { get; set; }

    [Column("condition_type")]
    [MaxLength(32)]
    public string ConditionType { get; set; } = string.Empty; // "threshold" | "rate_of_change" | "signal_loss" | "composite"

    [Column("condition_params", TypeName = "jsonb")]
    public string ConditionParams { get; set; } = "{}";

    [Column("hysteresis_minutes")]
    public int HysteresisMinutes { get; set; }

    [Column("confirmation_readings")]
    public int ConfirmationReadings { get; set; } = 1;

    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<AlertScheduleEntity> Schedules { get; set; } = [];
    public AlertTrackerStateEntity? TrackerState { get; set; }
}
```

**File: `AlertScheduleEntity.cs`**

```csharp
[Table("alert_schedules")]
public class AlertScheduleEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("name")]
    [MaxLength(128)]
    public string Name { get; set; } = "Default";

    [Column("is_default")]
    public bool IsDefault { get; set; }

    [Column("days_of_week", TypeName = "jsonb")]
    public string? DaysOfWeek { get; set; } // int[] as JSON, 0=Sun..6=Sat, null = all days

    [Column("start_time")]
    public TimeOnly? StartTime { get; set; }

    [Column("end_time")]
    public TimeOnly? EndTime { get; set; }

    [Column("timezone")]
    [MaxLength(64)]
    public string Timezone { get; set; } = "UTC";

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertRuleEntity? AlertRule { get; set; }
    public ICollection<AlertEscalationStepEntity> EscalationSteps { get; set; } = [];
}
```

**File: `AlertEscalationStepEntity.cs`**

```csharp
[Table("alert_escalation_steps")]
public class AlertEscalationStepEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_schedule_id")]
    public Guid AlertScheduleId { get; set; }

    [Column("step_order")]
    public int StepOrder { get; set; }

    [Column("delay_seconds")]
    public int DelaySeconds { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertScheduleEntity? AlertSchedule { get; set; }
    public ICollection<AlertStepChannelEntity> Channels { get; set; } = [];
}
```

**File: `AlertStepChannelEntity.cs`**

```csharp
[Table("alert_step_channels")]
public class AlertStepChannelEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    [Column("channel_type")]
    [MaxLength(32)]
    public string ChannelType { get; set; } = string.Empty;

    [Column("destination")]
    [MaxLength(512)]
    public string Destination { get; set; } = string.Empty;

    [Column("destination_label")]
    [MaxLength(128)]
    public string? DestinationLabel { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}
```

**File: `AlertTrackerStateEntity.cs`**

```csharp
[Table("alert_tracker_state")]
public class AlertTrackerStateEntity : ITenantScoped
{
    [Key]
    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("state")]
    [MaxLength(16)]
    public string State { get; set; } = "idle"; // "idle" | "confirming" | "active" | "hysteresis"

    [Column("confirmation_count")]
    public int ConfirmationCount { get; set; }

    [Column("active_excursion_id")]
    public Guid? ActiveExcursionId { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertRuleEntity? AlertRule { get; set; }
    public AlertExcursionEntity? ActiveExcursion { get; set; }
}
```

**File: `AlertExcursionEntity.cs`**

```csharp
[Table("alert_excursions")]
public class AlertExcursionEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_rule_id")]
    public Guid AlertRuleId { get; set; }

    [Column("started_at")]
    public DateTime StartedAt { get; set; }

    [Column("ended_at")]
    public DateTime? EndedAt { get; set; }

    [Column("acknowledged_at")]
    public DateTime? AcknowledgedAt { get; set; }

    [Column("acknowledged_by")]
    [MaxLength(256)]
    public string? AcknowledgedBy { get; set; }

    [Column("hysteresis_started_at")]
    public DateTime? HysteresisStartedAt { get; set; }

    // Navigation
    public AlertRuleEntity? AlertRule { get; set; }
    public ICollection<AlertInstanceEntity> Instances { get; set; } = [];
}
```

**File: `AlertInstanceEntity.cs`**

```csharp
[Table("alert_instances")]
public class AlertInstanceEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_excursion_id")]
    public Guid AlertExcursionId { get; set; }

    [Column("alert_schedule_id")]
    public Guid AlertScheduleId { get; set; }

    [Column("current_step_order")]
    public int CurrentStepOrder { get; set; }

    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "triggered"; // "triggered" | "escalating" | "acknowledged" | "resolved"

    [Column("triggered_at")]
    public DateTime TriggeredAt { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [Column("next_escalation_at")]
    public DateTime? NextEscalationAt { get; set; }

    // Navigation
    public AlertExcursionEntity? AlertExcursion { get; set; }
    public AlertScheduleEntity? AlertSchedule { get; set; }
}
```

**File: `AlertDeliveryEntity.cs`**

```csharp
[Table("alert_deliveries")]
public class AlertDeliveryEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("alert_instance_id")]
    public Guid AlertInstanceId { get; set; }

    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    [Column("channel_type")]
    [MaxLength(32)]
    public string ChannelType { get; set; } = string.Empty;

    [Column("destination")]
    [MaxLength(512)]
    public string Destination { get; set; } = string.Empty;

    [Column("payload", TypeName = "jsonb")]
    public string Payload { get; set; } = "{}";

    [Column("status")]
    [MaxLength(16)]
    public string Status { get; set; } = "pending"; // "pending" | "delivered" | "failed" | "expired"

    [Column("platform_message_id")]
    [MaxLength(256)]
    public string? PlatformMessageId { get; set; }

    [Column("platform_thread_id")]
    [MaxLength(256)]
    public string? PlatformThreadId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("delivered_at")]
    public DateTime? DeliveredAt { get; set; }

    [Column("retry_count")]
    public int RetryCount { get; set; }

    [Column("last_error")]
    public string? LastError { get; set; }

    // Navigation
    public AlertInstanceEntity? AlertInstance { get; set; }
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}
```

**File: `AlertInviteEntity.cs`**

```csharp
[Table("alert_invites")]
public class AlertInviteEntity : ITenantScoped
{
    [Key]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("created_by")]
    public Guid CreatedBy { get; set; }

    [Column("token")]
    [MaxLength(128)]
    public string Token { get; set; } = string.Empty;

    [Column("escalation_step_id")]
    public Guid EscalationStepId { get; set; }

    [Column("permission_scope")]
    [MaxLength(32)]
    public string PermissionScope { get; set; } = "view_acknowledge";

    [Column("is_used")]
    public bool IsUsed { get; set; }

    [Column("used_by")]
    public Guid? UsedBy { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public AlertEscalationStepEntity? EscalationStep { get; set; }
}
```

**Steps:**
1. Create all 9 entity files.
2. `dotnet build` — verify entities compile.
3. Commit: `feat: add new alert engine entities`

### Task 1.3: Register entities in DbContext and configure relationships

**File:** `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs`

**Steps:**
1. Add DbSets for all 9 new entities.
2. In `ConfigureEntities`, configure:
   - `AlertRuleEntity` — table name, JSONB for `condition_params`.
   - `AlertScheduleEntity` — FK to `AlertRuleEntity`, unique constraint on `(alert_rule_id, is_default)` where `is_default = true`.
   - `AlertEscalationStepEntity` — FK to `AlertScheduleEntity`.
   - `AlertStepChannelEntity` — FK to `AlertEscalationStepEntity`.
   - `AlertTrackerStateEntity` — PK is `AlertRuleId` (1:1 with rule), FK to `AlertExcursionEntity`.
   - `AlertExcursionEntity` — FK to `AlertRuleEntity`.
   - `AlertInstanceEntity` — FKs to `AlertExcursionEntity` and `AlertScheduleEntity`.
   - `AlertDeliveryEntity` — FKs to `AlertInstanceEntity` and `AlertEscalationStepEntity`.
   - `AlertInviteEntity` — FK to `AlertEscalationStepEntity`, unique index on `token`.
3. In `ConfigureIndexes`, add performance indexes:
   - `alert_instances`: `(status, next_escalation_at)` — for sweep query.
   - `alert_excursions`: `(tenant_id, ended_at)` — for active excursion lookup.
   - `alert_excursions`: `(alert_rule_id, ended_at)` — for excursion-per-rule lookup.
   - `alert_deliveries`: `(status, created_at)` — for pending delivery sweep.
   - `alert_invites`: `(token)` unique — for invite redemption lookup.
   - `tenants`: `(last_reading_at)` — for signal loss sweep.
4. Generate migration: `dotnet ef migrations add AddAlertEngineTables ...`
5. `dotnet build` and verify.
6. Commit: `feat: register alert engine entities in DbContext with indexes`

---

## Phase 2: Domain Models and Contracts

### Task 2.1: Create alert domain models

**File:** `src/Core/Nocturne.Core.Models/AlertModels.cs`

Define the domain-level models that the engine operates on. These are not entities — they're the models that services pass around.

```csharp
namespace Nocturne.Core.Models;

// Condition parameter types (deserialized from JSONB)
public record ThresholdCondition(string Direction, decimal Value);
public record RateOfChangeCondition(string Direction, decimal Rate);
public record SignalLossCondition(int TimeoutMinutes);
public record CompositeCondition(string Operator, List<ConditionNode> Conditions);
public record ConditionNode(string Type, ThresholdCondition? Threshold = null, RateOfChangeCondition? RateOfChange = null, SignalLossCondition? SignalLoss = null, CompositeCondition? Composite = null);

// Sensor context passed to evaluators
public record SensorContext
{
    public required decimal? LatestValue { get; init; }
    public required DateTime? LatestTimestamp { get; init; }
    public required decimal? TrendRate { get; init; } // mg/dL per minute
    public required DateTime? LastReadingAt { get; init; } // for signal loss
}

// Excursion tracker states
public enum TrackerState { Idle, Confirming, Active, Hysteresis }

// Alert payload (what delivery providers receive)
public record AlertPayload
{
    public required string AlertType { get; init; }
    public required string RuleName { get; init; }
    public required decimal? GlucoseValue { get; init; }
    public required string? Trend { get; init; }
    public required decimal? TrendRate { get; init; }
    public required DateTime ReadingTimestamp { get; init; }
    public required Guid ExcursionId { get; init; }
    public required Guid InstanceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string SubjectName { get; init; }
    public required int ActiveExcursionCount { get; init; }
}
```

**Steps:**
1. Create the file.
2. `dotnet build` — verify.
3. Commit: `feat: add alert engine domain models`

### Task 2.2: Create alert service contracts

**File:** `src/Core/Nocturne.Core.Contracts/Alerts/IConditionEvaluator.cs`

```csharp
namespace Nocturne.Core.Contracts.Alerts;

public interface IConditionEvaluator
{
    string ConditionType { get; }
    bool Evaluate(string conditionParamsJson, SensorContext context);
}
```

**File:** `src/Core/Nocturne.Core.Contracts/Alerts/IExcursionTracker.cs`

```csharp
namespace Nocturne.Core.Contracts.Alerts;

public interface IExcursionTracker
{
    /// <summary>
    /// Feed a boolean evaluation result into the tracker for a specific rule.
    /// Returns the state transition that occurred (if any) so the engine can act on it.
    /// </summary>
    Task<ExcursionTransition> ProcessEvaluationAsync(Guid alertRuleId, bool conditionMet, CancellationToken ct);
}

public enum ExcursionTransitionType { None, ExcursionOpened, ExcursionContinues, HysteresisStarted, HysteresisResumed, ExcursionClosed }

public record ExcursionTransition(ExcursionTransitionType Type, Guid? ExcursionId = null);
```

**File:** `src/Core/Nocturne.Core.Contracts/Alerts/IAlertOrchestrator.cs` (new version)

```csharp
namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertOrchestrator
{
    /// <summary>
    /// Evaluate all enabled rules for the current tenant against the latest sensor data.
    /// Called by the glucose ingest pipeline on each new reading.
    /// </summary>
    Task EvaluateAsync(SensorContext context, CancellationToken ct);
}
```

**File:** `src/Core/Nocturne.Core.Contracts/Alerts/IAlertDeliveryService.cs`

```csharp
namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertDeliveryService
{
    Task DispatchAsync(Guid alertInstanceId, int stepOrder, AlertPayload payload, CancellationToken ct);
    Task MarkDeliveredAsync(Guid deliveryId, string? platformMessageId, string? platformThreadId, CancellationToken ct);
    Task MarkFailedAsync(Guid deliveryId, string error, CancellationToken ct);
}
```

**File:** `src/Core/Nocturne.Core.Contracts/Alerts/IAlertAcknowledgementService.cs`

```csharp
namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertAcknowledgementService
{
    Task AcknowledgeAllAsync(Guid tenantId, string acknowledgedBy, CancellationToken ct);
}
```

**Steps:**
1. Create/replace the contract files in `src/Core/Nocturne.Core.Contracts/Alerts/`.
2. `dotnet build` — verify.
3. Commit: `feat: add alert engine service contracts`

---

## Phase 3: Condition Evaluators

### Task 3.1: Write tests for condition evaluators

**File:** `tests/Unit/Nocturne.API.Tests/Services/Alerts/ConditionEvaluatorTests.cs`

Write unit tests covering:
- **ThresholdEvaluator**: `below` direction triggers when value < threshold, `above` when value > threshold, null value returns false.
- **RateOfChangeEvaluator**: `falling` direction triggers when rate <= -threshold, `rising` when rate >= threshold, null rate returns false.
- **SignalLossEvaluator**: triggers when `now - lastReadingAt > timeoutMinutes`, does not trigger when within window, null `lastReadingAt` triggers immediately.
- **CompositeEvaluator**: AND requires all children true, OR requires any child true, nested composites work.

**Steps:**
1. Write the test file with all test cases (they will not compile yet).
2. Commit: `test: add condition evaluator tests (red)`

### Task 3.2: Implement condition evaluators

**Files to create in `src/API/Nocturne.API/Services/Alerts/Evaluators/`:**
- `ThresholdEvaluator.cs`
- `RateOfChangeEvaluator.cs`
- `SignalLossEvaluator.cs`
- `CompositeEvaluator.cs`
- `ConditionEvaluatorRegistry.cs` — resolves `condition_type` string to the correct `IConditionEvaluator`.

Each evaluator implements `IConditionEvaluator`. The registry is registered as a singleton (or scoped) and injected where needed. `CompositeEvaluator` depends on the registry to recurse.

**Steps:**
1. Implement all evaluator classes.
2. Register in DI (in `AddAlertingAndMonitoring`).
3. Run tests: `dotnet test --filter "ConditionEvaluator"` — all should pass.
4. Commit: `feat: implement condition evaluators (threshold, rate, signal loss, composite)`

---

## Phase 4: Excursion Tracker

### Task 4.1: Write tests for the ExcursionTracker state machine

**File:** `tests/Unit/Nocturne.API.Tests/Services/Alerts/ExcursionTrackerTests.cs`

Test the full state machine from the design doc:
- IDLE → CONFIRMING on first `true`.
- CONFIRMING → ACTIVE after `confirmation_readings` consecutive `true` values.
- CONFIRMING → IDLE on any `false`.
- ACTIVE → HYSTERESIS on first `false`.
- HYSTERESIS → ACTIVE on `true` before expiry.
- HYSTERESIS → IDLE after `hysteresis_minutes` of sustained `false`.
- Confirmation counter persists across calls.
- Excursion ID is created on ACTIVE transition and returned.
- Acknowledged excursions in HYSTERESIS→ACTIVE do not re-fire alerts.

These tests should mock the database layer (tracker state repository).

**Steps:**
1. Write the test file.
2. Commit: `test: add excursion tracker state machine tests (red)`

### Task 4.2: Implement ExcursionTracker

**File:** `src/API/Nocturne.API/Services/Alerts/ExcursionTracker.cs`

Implements `IExcursionTracker`. On each call:
1. Load `AlertTrackerStateEntity` for the rule (with row lock in production — the service layer handles the transaction).
2. Read current state.
3. Apply state machine logic based on `conditionMet` boolean.
4. Persist updated state.
5. Create/close `AlertExcursionEntity` records as needed.
6. Return `ExcursionTransition` describing what happened.

Also needs a repository/data access layer for tracker state and excursions. Create:
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/AlertTrackerStateRepository.cs`
- `src/Infrastructure/Nocturne.Infrastructure.Data/Repositories/AlertExcursionRepository.cs`

**Steps:**
1. Implement the tracker and repositories.
2. Register in DI.
3. Run tests — all should pass.
4. Commit: `feat: implement ExcursionTracker state machine`

---

## Phase 5: Alert Engine Core

### Task 5.1: Implement the AlertOrchestrator (event-driven path)

**File:** `src/API/Nocturne.API/Services/Alerts/AlertOrchestrator.cs` (new implementation)

Implements `IAlertOrchestrator`. On each call (triggered by new glucose data):
1. Load all enabled `AlertRuleEntity` records for the current tenant (via tenant accessor).
2. For each rule:
   a. Resolve the condition evaluator from the registry.
   b. Evaluate the condition against the `SensorContext`.
   c. Feed the result into `IExcursionTracker.ProcessEvaluationAsync`.
   d. If `ExcursionOpened`: determine active schedule, create `AlertInstanceEntity`, write delivery requests for step 0, set `next_escalation_at`.
   e. If `ExcursionClosed`: resolve alert instance, cancel pending deliveries, broadcast `alert_resolved`.

**Steps:**
1. Implement `AlertOrchestrator`.
2. Create repository methods for loading rules, creating instances, writing deliveries.
3. Register in DI (`AddAlertingAndMonitoring`).
4. Re-wire `GlucosePublisher` to call the new `IAlertOrchestrator.EvaluateAsync`.
5. Re-wire `SensorGlucoseController` and entries controllers similarly.
6. Write integration-style unit tests using mocked repositories.
7. `dotnet build` — verify.
8. Commit: `feat: implement AlertOrchestrator (event-driven evaluation loop)`

### Task 5.2: Implement the AlertSweepService (periodic background service)

**File:** `src/API/Nocturne.API/Services/Alerts/AlertSweepService.cs`

A `BackgroundService` that runs every 30 seconds and performs three operations:
1. **AdvanceEscalations**: Query `alert_instances WHERE status = 'escalating' AND next_escalation_at <= now()`. For each, advance to next step, write delivery requests, update `next_escalation_at`.
2. **CloseHysteresisWindows**: Query `alert_excursions WHERE ended_at IS NULL AND hysteresis_started_at IS NOT NULL AND hysteresis_started_at + hysteresis_minutes <= now()`. Close each excursion, resolve instances.
3. **EvaluateSignalLoss**: Query `tenants WHERE last_reading_at < now() - timeout` cross-referenced with signal loss rules. Feed `true` into the ExcursionTracker for matching rules.

Follows the existing `IServiceScopeFactory` + `ITenantAccessor` pattern used by `ConnectorBackgroundService` and `NotificationResolutionService`.

**Steps:**
1. Implement `AlertSweepService`.
2. Register as `IHostedService` in `AddAlertingAndMonitoring`.
3. Write unit tests for the three sweep operations.
4. `dotnet build` — verify.
5. Commit: `feat: implement AlertSweepService (escalation, hysteresis, signal loss)`

### Task 5.3: Implement schedule resolution

**File:** `src/API/Nocturne.API/Services/Alerts/ScheduleResolver.cs`

A helper that, given a rule's schedules and the current time in the tenant's timezone, returns the active schedule. Logic:
1. Filter schedules by `days_of_week` (if set).
2. Filter by `start_time`/`end_time` window.
3. If no non-default schedule matches, return the default.

**Steps:**
1. Write tests for schedule resolution (various time-of-day and day-of-week combos, cross-midnight windows).
2. Implement `ScheduleResolver`.
3. Run tests — verify.
4. Commit: `feat: implement schedule resolver for time-of-day routing`

---

## Phase 6: Delivery Dispatch

### Task 6.1: Implement AlertDeliveryService

**File:** `src/API/Nocturne.API/Services/Alerts/AlertDeliveryService.cs`

Implements `IAlertDeliveryService`. Dual-channel delivery:

1. **Write to DB**: Create `AlertDeliveryEntity` records with `status = "pending"` for each channel in the escalation step.
2. **Fast path (SignalR)**: Publish an `alert_dispatch` event on the SignalR hub so connected clients receive it instantly.
3. **Delivery callback**: `MarkDeliveredAsync` and `MarkFailedAsync` update the delivery record.

**File:** `src/API/Nocturne.API/Services/Alerts/AlertDeliveryReliabilitySweep.cs`

A method called by `AlertSweepService` (or its own `BackgroundService`) that picks up deliveries stuck in `pending` for >10 seconds and re-publishes them.

**Steps:**
1. Implement `AlertDeliveryService`.
2. Implement the reliability sweep.
3. Write unit tests.
4. Register in DI.
5. Commit: `feat: implement dual-channel alert delivery dispatch`

### Task 6.2: Implement WebPush and Webhook delivery providers

**File:** `src/API/Nocturne.API/Services/Alerts/Providers/WebPushProvider.cs`

Listens for `alert_dispatch` events on SignalR. For `web_push` channel type, broadcasts to the tenant's alarm subscribers group via the existing `ISignalRBroadcastService`.

**File:** `src/API/Nocturne.API/Services/Alerts/Providers/WebhookProvider.cs`

For `webhook` channel type, sends HTTP POST to the destination URL with the alert payload. Reuse the existing `WebhookRequestSender` and `WebhookSignature` infrastructure from `src/API/Nocturne.API/Services/Alerts/Webhooks/`.

**Steps:**
1. Implement both providers.
2. Register in DI.
3. Write tests (mock HTTP for webhook, mock SignalR for web push).
4. Commit: `feat: implement web_push and webhook delivery providers`

---

## Phase 7: Acknowledgement

### Task 7.1: Implement acknowledgement service

**File:** `src/API/Nocturne.API/Services/Alerts/AlertAcknowledgementService.cs`

Implements `IAlertAcknowledgementService`. When called:
1. Set `acknowledged_at` and `acknowledged_by` on ALL active excursions for the tenant.
2. Set status to `acknowledged` on all corresponding alert instances.
3. Clear `next_escalation_at` — no further escalation.
4. Do NOT close excursions (they remain open until hysteresis closes them).
5. Broadcast `alert_acknowledged` event via SignalR.

**Steps:**
1. Write tests covering: ack all excursions for tenant, no re-fire after ack, escalation stops.
2. Implement the service.
3. Register in DI.
4. Commit: `feat: implement tenant-wide alert acknowledgement`

---

## Phase 8: API Endpoints

### Task 8.1: Alert Rules CRUD controller

**File:** `src/API/Nocturne.API/Controllers/V4/AlertRulesController.cs`

Route: `api/v4/alert-rules`

Endpoints:
- `GET /` — List all rules for the current tenant (include schedules and steps).
- `GET /{id}` — Get a single rule with full schedule/escalation tree.
- `POST /` — Create a rule (with nested schedules, steps, channels).
- `PUT /{id}` — Update a rule.
- `DELETE /{id}` — Delete a rule (cascade deletes schedules, steps, channels, tracker state).
- `PATCH /{id}/toggle` — Enable/disable a rule.

Use the `[RemoteQuery]` attribute for GET endpoints to generate NSwag types.

**Steps:**
1. Implement the controller.
2. Create necessary repository methods (or use EF directly with includes).
3. Add DTOs (request/response models) — these should be in the controller file or a nearby DTOs file, and NSwag will generate the frontend types.
4. `dotnet build` — verify.
5. Commit: `feat: add alert rules CRUD API (v4)`

### Task 8.2: Alert state and acknowledgement endpoints

**File:** `src/API/Nocturne.API/Controllers/V4/AlertsController.cs`

Route: `api/v4/alerts`

Endpoints:
- `GET /active` — List active excursions for the current tenant (with instances).
- `GET /history` — Paginated alert history (closed excursions).
- `POST /tenants/{tenantId}/acknowledge` — Acknowledge all active excursions for a tenant.
- `GET /deliveries/{instanceId}` — Delivery status for a specific instance.

**Steps:**
1. Implement the controller.
2. `dotnet build` — verify.
3. Commit: `feat: add alert state and acknowledgement API (v4)`

### Task 8.3: Alert invite endpoints

**File:** `src/API/Nocturne.API/Controllers/V4/AlertInvitesController.cs`

Route: `api/v4/alert-invites`

Endpoints:
- `POST /` — Generate an invite link (returns token and URL).
- `GET /{token}` — Validate an invite (used by the redemption flow).
- `POST /{token}/redeem` — Redeem an invite (assigns the current user to the escalation step).
- `DELETE /{id}` — Revoke an unredeemed invite.

**Steps:**
1. Implement the controller.
2. Add invite token generation (cryptographically random, URL-safe).
3. `dotnet build` — verify.
4. Commit: `feat: add alert invite endpoints (v4)`

---

## Phase 9: SignalR Hub Updates

### Task 9.1: Create AlertHub for real-time alert events

**File:** `src/API/Nocturne.API/Hubs/AlertHub.cs`

A new `TenantAwareHub` that replaces the old `AlarmHub` for the new alert system. Methods:
- `Subscribe()` — Join the tenant's alert group.
- `Acknowledge()` — Acknowledge all alerts (delegates to `IAlertAcknowledgementService`).

Events broadcast to clients:
- `alert_dispatch` — New alert delivery.
- `alert_acknowledged` — All alerts acknowledged (includes who acknowledged).
- `alert_resolved` — Excursion closed.

**Steps:**
1. Implement `AlertHub`.
2. Register: `app.MapHub<AlertHub>("/hubs/alerts")` in Program.cs.
3. Keep the old `AlarmHub` mapped at `/hubs/alarms` for Nightscout socket.io compat (it doesn't conflict).
4. `dotnet build` — verify.
5. Commit: `feat: add AlertHub for real-time alert events`

---

## Phase 10: Frontend — Alert Settings & Setup Wizard

### Task 10.1: Generate NSwag client with new alert types

**Steps:**
1. Run `aspire run` to regenerate the NSwag TypeScript client.
2. Verify the new alert DTOs appear in the generated client.
3. Commit: `chore: regenerate NSwag client with alert engine types`

### Task 10.2: Create alert settings route and remote functions

**File:** `src/Web/packages/app/src/routes/settings/alerts/+page.svelte`

Replace the old `settings/alarms/+page.svelte` with a new alerts settings page. This is the "full settings" view for power users — it shows all rules, schedules, and escalation chains.

**Remote functions file:** `src/Web/packages/app/src/lib/remote/alerts.remote.ts`

Functions:
- `getAlertRules()` — Fetch all rules.
- `getAlertRule(id)` — Fetch a single rule.
- `createAlertRule(data)` — Create.
- `updateAlertRule(id, data)` — Update.
- `deleteAlertRule(id)` — Delete.
- `toggleAlertRule(id)` — Toggle enable/disable.
- `getActiveAlerts()` — Fetch active excursions.
- `acknowledgeAlerts(tenantId)` — Acknowledge all.
- `getAlertHistory(page, pageSize)` — Paginated history.

**Page layout:**
- List of alert rules as cards.
- Each card shows: name, condition summary (human-readable), enabled toggle, schedule count.
- Click to expand/edit: full schedule and escalation chain editor.
- "Add Rule" button opens a creation form.
- Active alerts banner at the top with acknowledge button.

**Steps:**
1. Create the remote functions file.
2. Create the settings page.
3. Delete or redirect the old `settings/alarms/+page.svelte`.
4. `pnpm run check` in the app package — verify types.
5. Commit: `feat: add alert settings page with rule management`

### Task 10.3: Build the alert setup wizard

**File:** `src/Web/packages/app/src/routes/setup/alerts/+page.svelte`

The wizard is the first-time setup experience. It's a step-by-step flow:

**Step 1: Choose presets**
Present the 6 preset alert types from the design doc (Urgent Low, Low, Fast Drop, High, Urgent High, Sensor Lost). Each is a toggle card with adjustable threshold. Values shown in the user's preferred unit (mg/dL or mmol/L), converted to mg/dL on save.

**Step 2: Choose delivery channels**
For V1: web push toggle and webhook URL input. Show placeholders for future channels (Discord, Telegram, etc.) as "coming soon."

**Step 3: Escalation chain**
Simple version: Step 0 = immediate to the user. Option to add Step 1 with a delay and invite link generation. The invite flow is: enter a label ("Mum"), choose delay ("after 5 minutes"), generate invite link, copy/share.

**Step 4: Review and save**
Summary of all rules, channels, and escalation steps. Save button creates all rules via the API.

**Medical disclaimer** shown before save (text from design doc).

**Steps:**
1. Create the wizard page with step navigation.
2. Build preset selection component.
3. Build channel configuration component.
4. Build escalation chain builder component.
5. Build review/save step.
6. Wire up to remote functions.
7. `pnpm run check` — verify.
8. Commit: `feat: add alert setup wizard`

### Task 10.4: Alert notification display in the main UI

**File:** `src/Web/packages/app/src/lib/components/alerts/AlertBanner.svelte`

A persistent banner component that:
- Connects to `AlertHub` via SignalR.
- Shows active alerts with glucose value, rule name, subject name.
- Has an Acknowledge button.
- Updates in real-time as alerts are acknowledged/resolved.

Integrate this into the main layout so it appears at the top of the page when alerts are active.

**Steps:**
1. Create the `AlertBanner` component.
2. Create a SignalR connection helper for the alert hub.
3. Add to the main layout.
4. `pnpm run check` — verify.
5. Commit: `feat: add real-time alert banner with acknowledge`

### Task 10.5: Update navigation

Add "Alerts" to the settings navigation. Remove or redirect "Alarms" to "Alerts."

If there's a setup detection mechanism (no rules configured yet), redirect to the wizard.

**Steps:**
1. Update the settings navigation component.
2. Remove old alarms route.
3. Commit: `feat: update navigation for alert engine`

---

## Phase 11: Integration Testing

### Task 11.1: End-to-end alert evaluation test

**File:** `tests/Unit/Nocturne.API.Tests/Services/Alerts/AlertEngineIntegrationTests.cs`

An integration-style test (still using mocks for DB, but wiring the full pipeline):
1. Create a rule with a threshold condition.
2. Send a glucose reading that triggers the condition.
3. Verify: excursion created, instance created, delivery written.
4. Send a reading that clears the condition.
5. Verify: hysteresis started.
6. Wait for hysteresis to expire.
7. Verify: excursion closed, instance resolved.

### Task 11.2: Escalation advancement test

Test that the sweep service correctly advances escalation steps after the delay expires.

### Task 11.3: Acknowledgement test

Test that acknowledging stops escalation and marks all excursions for the tenant.

**Steps:**
1. Write all integration tests.
2. Run: `dotnet test --filter "AlertEngine"` — all should pass.
3. Commit: `test: add alert engine integration tests`

---

## Phase 12: Final Wiring and Validation

### Task 12.1: Verify full pipeline with Aspire

**Steps:**
1. Run `aspire run`.
2. Verify all resources start cleanly.
3. Verify migrations apply (new tables created, old tables dropped).
4. Create an alert rule via the API.
5. Submit a glucose reading and verify an alert is triggered.
6. Acknowledge the alert and verify escalation stops.
7. Fix any issues found.
8. Commit any fixes.

### Task 12.2: Cleanup and final review

**Steps:**
1. Remove any dead imports, unused `using` statements.
2. Verify no old alert references remain: `grep -r "AlertHistoryEntity\|AlertMonitoringOptions\|IAlertRulesEngine\|IAlertProcessingService" src/`.
3. Run full test suite: `dotnet test --filter "Category!=Integration&Category!=Performance"`.
4. Run frontend type check: `cd src/Web/packages/app && pnpm run check`.
5. Final commit: `chore: alert engine cleanup and verification`

---

## Dependency Graph

```
Phase 0 (cleanup) → Phase 1 (schema) → Phase 2 (models/contracts)
                                              ↓
                              ┌────────────────┼────────────────┐
                              ↓                ↓                ↓
                        Phase 3          Phase 4          Phase 5.3
                     (evaluators)    (excursion tracker)  (schedule resolver)
                              ↓                ↓                ↓
                              └────────────────┼────────────────┘
                                               ↓
                                         Phase 5.1-5.2
                                    (orchestrator + sweep)
                                               ↓
                                    ┌──────────┼──────────┐
                                    ↓          ↓          ↓
                              Phase 6    Phase 7    Phase 9
                            (delivery)   (ack)     (SignalR)
                                    ↓          ↓          ↓
                                    └──────────┼──────────┘
                                               ↓
                                          Phase 8
                                        (API endpoints)
                                               ↓
                                         Phase 10
                                        (frontend)
                                               ↓
                                        Phase 11-12
                                    (integration + validation)
```

## Parallelisation Opportunities

After Phase 2 completes, the following can run in parallel:
- **Phase 3** (evaluators) — pure functions, no dependencies on tracker or orchestrator
- **Phase 4** (excursion tracker) — depends only on models and entities
- **Phase 5.3** (schedule resolver) — pure function, no dependencies

After Phase 5 completes:
- **Phase 6** (delivery), **Phase 7** (acknowledgement), and **Phase 9** (SignalR) can all run in parallel.

After Phase 8 (API endpoints):
- **Task 10.2-10.5** (frontend components) can be parallelised.

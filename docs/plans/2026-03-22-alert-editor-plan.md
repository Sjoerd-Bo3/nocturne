# Alert Rule Editor, Client Config & Quiet Hours Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a full rule editor sheet, per-rule client presentation config (audio/visual/snooze with smart snooze), custom sound upload, and tenant-level quiet hours to the alert engine.

**Architecture:** Schema additions to existing alert entities + new custom sounds table, new API endpoints for sounds/snooze/quiet-hours, server-side quiet hours + smart snooze logic in the sweep, and a Svelte 5 sheet component with tabbed editing for the frontend.

**Tech Stack:** .NET 10, EF Core + PostgreSQL (JSONB + bytea), xUnit + FluentAssertions + Moq, SvelteKit 2 + Svelte 5 + shadcn-svelte.

**Reference:** [alert-editor-design.md](2026-03-22-alert-editor-design.md) is the authoritative design document.

---

## Phase 1: Schema Changes

### Task 1.1: Add severity and client_configuration to AlertRuleEntity

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AlertRuleEntity.cs`
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs`

**Steps:**

1. Add two fields to `AlertRuleEntity`:
   ```csharp
   [Column("severity")]
   [MaxLength(16)]
   public string Severity { get; set; } = "normal"; // "normal" | "critical"

   [Column("client_configuration", TypeName = "jsonb")]
   public string ClientConfiguration { get; set; } = "{}";
   ```

2. In `NocturneDbContext.ConfigureEntities`, add JSONB default for `client_configuration`:
   ```csharp
   entity.Property(e => e.ClientConfiguration)
       .HasColumnType("jsonb")
       .HasDefaultValue("{}");
   ```

3. `dotnet build src/Infrastructure/Nocturne.Infrastructure.Data -p:GenerateNSwagClient=false --verbosity quiet`

4. Commit: `feat: add severity and client_configuration to AlertRuleEntity`

### Task 1.2: Add snoozed_until and snooze_count to AlertInstanceEntity

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AlertInstanceEntity.cs`

**Steps:**

1. Add two fields to `AlertInstanceEntity`:
   ```csharp
   [Column("snoozed_until")]
   public DateTime? SnoozedUntil { get; set; }

   [Column("snooze_count")]
   public int SnoozeCount { get; set; }
   ```

2. Build to verify.

3. Commit: `feat: add snoozed_until and snooze_count to AlertInstanceEntity`

### Task 1.3: Add quiet hours fields to TenantEntity

**Files:**
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/TenantEntity.cs`

**Steps:**

1. Add three fields to `TenantEntity`:
   ```csharp
   [Column("quiet_hours_start")]
   public TimeOnly? QuietHoursStart { get; set; }

   [Column("quiet_hours_end")]
   public TimeOnly? QuietHoursEnd { get; set; }

   [Column("quiet_hours_override_critical")]
   public bool QuietHoursOverrideCritical { get; set; } = true;
   ```

2. Build to verify.

3. Commit: `feat: add quiet hours fields to TenantEntity`

### Task 1.4: Create AlertCustomSoundEntity

**Files:**
- Create: `src/Infrastructure/Nocturne.Infrastructure.Data/Entities/AlertCustomSoundEntity.cs`
- Modify: `src/Infrastructure/Nocturne.Infrastructure.Data/NocturneDbContext.cs`

**Steps:**

1. Create entity:
   ```csharp
   [Table("alert_custom_sounds")]
   public class AlertCustomSoundEntity : ITenantScoped
   {
       [Key]
       public Guid Id { get; set; }

       [Column("tenant_id")]
       public Guid TenantId { get; set; }

       [Column("name")]
       [MaxLength(128)]
       public string Name { get; set; } = string.Empty;

       [Column("mime_type")]
       [MaxLength(64)]
       public string MimeType { get; set; } = string.Empty;

       [Column("data")]
       public byte[] Data { get; set; } = [];

       [Column("file_size")]
       public int FileSize { get; set; }

       [Column("created_at")]
       public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
   }
   ```

2. Add DbSet to NocturneDbContext:
   ```csharp
   public DbSet<AlertCustomSoundEntity> AlertCustomSounds { get; set; }
   ```

3. Configure in `ConfigureEntities` (table name, file size check constraint if desired).

4. Build to verify.

5. Commit: `feat: add AlertCustomSoundEntity`

### Task 1.5: Generate migration

**Steps:**

1. Build: `dotnet build src/API/Nocturne.API -p:GenerateNSwagClient=false`

2. Generate migration:
   ```
   dotnet ef migrations add AddAlertEditorFields \
     --project src/Infrastructure/Nocturne.Infrastructure.Data \
     --startup-project src/API/Nocturne.API \
     --no-build
   ```

3. Verify the migration adds: `severity`, `client_configuration` on `alert_rules`; `snoozed_until`, `snooze_count` on `alert_instances`; `quiet_hours_start`, `quiet_hours_end`, `quiet_hours_override_critical` on `tenants`; new `alert_custom_sounds` table.

4. Commit: `migrate: add alert editor fields and custom sounds table`

---

## Phase 2: Backend — Update Existing Endpoints

### Task 2.1: Update AlertRulesController DTOs

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V4/AlertRulesController.cs`

**Steps:**

1. Add to `AlertRuleResponse`:
   ```csharp
   public string Severity { get; set; } = "normal";
   public object ClientConfiguration { get; set; } = new { };
   ```

2. Add to `CreateAlertRuleRequest`:
   ```csharp
   public string? Severity { get; set; }
   public object? ClientConfiguration { get; set; }
   ```

3. Add to `UpdateAlertRuleRequest`:
   ```csharp
   public string? Severity { get; set; }
   public object? ClientConfiguration { get; set; }
   ```

4. Update `MapToResponse` to include both new fields:
   ```csharp
   Severity = entity.Severity,
   ClientConfiguration = DeserializeJson(entity.ClientConfiguration),
   ```

5. Update `CreateRule` to set both fields on the entity:
   ```csharp
   Severity = request.Severity ?? "normal",
   ClientConfiguration = request.ClientConfiguration is not null
       ? JsonSerializer.Serialize(request.ClientConfiguration)
       : "{}",
   ```

6. Update `UpdateRule` similarly.

7. Build and verify.

8. Commit: `feat: add severity and clientConfiguration to alert rule DTOs`

---

## Phase 3: Backend — New Endpoints

### Task 3.1: Custom Sounds Controller

**Files:**
- Create: `src/API/Nocturne.API/Controllers/V4/AlertCustomSoundsController.cs`

**Steps:**

1. Create controller at route `api/v4/alert-sounds`:

   - `POST /` — upload a sound. Accept `IFormFile`. Validate: max 500KB (`if (file.Length > 512_000) return BadRequest(...)`), must be `audio/*` content type. Store raw bytes in `AlertCustomSoundEntity`. Return `AlertCustomSoundResponse` (id, name, mimeType, fileSize, createdAt). Attributes: `[RemoteCommand]`, `[RequestSizeLimit(512_000)]`.

   - `GET /` — list all custom sounds for tenant. Return `List<AlertCustomSoundResponse>` (no `data` field — just metadata). Attribute: `[RemoteQuery]`.

   - `GET /{id}/stream` — stream audio. Load entity, return `File(entity.Data, entity.MimeType)` with `Response.Headers["Cache-Control"] = "max-age=86400"`. No `[RemoteQuery]` (this is a raw file download, not a JSON endpoint).

   - `DELETE /{id}` — delete a sound. Return 204. Attribute: `[RemoteCommand(Invalidates = ["GetSounds"])]`.

2. Define `AlertCustomSoundResponse`:
   ```csharp
   public class AlertCustomSoundResponse
   {
       public Guid Id { get; set; }
       public string Name { get; set; } = string.Empty;
       public string MimeType { get; set; } = string.Empty;
       public int FileSize { get; set; }
       public DateTime CreatedAt { get; set; }
   }
   ```

3. Build and verify.

4. Commit: `feat: add custom sounds CRUD and streaming endpoint`

### Task 3.2: Quiet Hours Endpoints

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V4/AlertsController.cs`

**Steps:**

1. Add to `AlertsController`:

   ```csharp
   [HttpGet("quiet-hours")]
   [RemoteQuery]
   public async Task<ActionResult<QuietHoursResponse>> GetQuietHours(CancellationToken ct)
   ```
   Load the tenant entity (via `IDbContextFactory`, ignoring query filters with `IgnoreQueryFilters()` or loading from tenant accessor), return quiet hours fields.

   ```csharp
   [HttpPut("quiet-hours")]
   [RemoteCommand(Invalidates = ["GetQuietHours"])]
   public async Task<ActionResult<QuietHoursResponse>> UpdateQuietHours(
       [FromBody] UpdateQuietHoursRequest request, CancellationToken ct)
   ```
   Update the tenant's quiet hours fields.

2. Define DTOs:
   ```csharp
   public class QuietHoursResponse
   {
       public bool Enabled { get; set; }
       public string? StartTime { get; set; } // "HH:mm"
       public string? EndTime { get; set; }   // "HH:mm"
       public bool OverrideCritical { get; set; }
   }

   public class UpdateQuietHoursRequest
   {
       public bool Enabled { get; set; }
       public string? StartTime { get; set; }
       public string? EndTime { get; set; }
       public bool OverrideCritical { get; set; } = true;
   }
   ```

   When `Enabled` is false, set `QuietHoursStart` and `QuietHoursEnd` to null on the tenant. When true, parse the time strings.

3. Build and verify.

4. Commit: `feat: add quiet hours GET/PUT endpoints`

### Task 3.3: Snooze Endpoint

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/V4/AlertsController.cs`

**Steps:**

1. Add to `AlertsController`:

   ```csharp
   [HttpPost("instances/{instanceId:guid}/snooze")]
   [RemoteCommand(Invalidates = ["GetActiveAlerts"])]
   public async Task<ActionResult> SnoozeInstance(
       Guid instanceId, [FromBody] SnoozeRequest request, CancellationToken ct)
   ```

   Logic:
   - Load the `AlertInstanceEntity` with its excursion and rule.
   - Deserialize the rule's `client_configuration` to get snooze options.
   - Validate `request.Minutes` is in `snooze.options` array → 400 if not.
   - Validate `instance.SnoozeCount < snooze.maxCount` → 409 if at max.
   - Set `instance.SnoozedUntil = DateTime.UtcNow.AddMinutes(request.Minutes)`.
   - Increment `instance.SnoozeCount`.
   - Save and return 204.

2. Define DTO:
   ```csharp
   public class SnoozeRequest
   {
       public int Minutes { get; set; }
   }
   ```

3. Build and verify.

4. Commit: `feat: add snooze endpoint with validation`

---

## Phase 4: Backend — Server-Side Behavior

### Task 4.1: Quiet hours check in AlertDeliveryService

**Files:**
- Modify: `src/API/Nocturne.API/Services/Alerts/AlertDeliveryService.cs`

**Steps:**

1. Add a quiet hours check at the start of `DispatchAsync`:
   - Load the tenant entity to get quiet hours config.
   - Load the alert rule (from the instance → excursion → rule chain) to get `severity`.
   - If quiet hours are active (start/end set, current time in tenant timezone is within the window):
     - If `severity == "critical"` AND `quiet_hours_override_critical` → proceed (bypass).
     - Else → skip dispatch, log "Dispatch suppressed by quiet hours."
   - This requires injecting `IDbContextFactory` and knowing the tenant ID.

2. Build and verify.

3. Commit: `feat: check quiet hours before alert dispatch`

### Task 4.2: Smart snooze in AlertSweepService

**Files:**
- Modify: `src/API/Nocturne.API/Services/Alerts/AlertSweepService.cs`

**Steps:**

1. Add a fourth operation in the sweep loop: `CheckSnoozedInstancesAsync(ct)`.

2. Logic:
   - Query: `AlertInstances WHERE SnoozedUntil IS NOT NULL AND SnoozedUntil <= now() AND Status != 'resolved' AND Status != 'acknowledged'`
   - For each expired snooze:
     a. Load the rule's `client_configuration`, deserialize snooze config.
     b. If `smartSnooze` is enabled AND `instance.SnoozeCount < snooze.maxCount`:
        - Determine if trend is favorable:
          - Load latest `SensorContext` for the tenant (latest glucose, trend rate).
          - For rules with condition_type "threshold" and direction "below": favorable if trend rate > 0 (rising).
          - For rules with condition_type "threshold" and direction "above": favorable if trend rate < 0 (falling).
          - For other condition types: not favorable (don't extend).
        - If favorable: extend `SnoozedUntil += smartSnoozeExtendMinutes`, increment `SnoozeCount`, log.
        - If not favorable: clear `SnoozedUntil`, resume escalation (don't touch `SnoozeCount`), log.
     c. If smart snooze disabled or max count reached: clear `SnoozedUntil`, resume escalation.

3. Also update `AdvanceEscalationsAsync` to skip snoozed instances:
   - Add `AND SnoozedUntil IS NULL` to the escalation advancement query.

4. Build and verify.

5. Commit: `feat: add smart snooze and snooze-aware escalation to sweep`

---

## Phase 5: Frontend — Rule Editor Sheet

### Task 5.1: Create the RuleEditorSheet component

**Files:**
- Create: `src/Web/packages/app/src/lib/components/alerts/RuleEditorSheet.svelte`

**Steps:**

1. Create a Sheet component (from shadcn-svelte) that opens from the right side.

2. Props:
   - `rule`: `AlertRuleResponse | null` (null = create mode)
   - `open`: boolean (bindable)
   - `onSave`: callback to refresh the rule list

3. Internal state:
   - Form fields initialized from `rule` (or defaults for create mode)
   - Active tab: `"general"` | `"presentation"` | `"snooze"` | `"schedules"`
   - Saving state

4. Use Tabs component from shadcn-svelte for the 4 tabs.

5. **General tab:**
   - Name: text Input
   - Description: text Input
   - Severity: Select (normal / critical)
   - Condition type: Select (threshold / rate_of_change / signal_loss)
     - If composite: show read-only summary badge, disable editing
   - Dynamic condition params based on type:
     - Threshold: direction Select (below/above) + value Input (number, label "mg/dL")
     - Rate of change: direction Select (falling/rising) + rate Input (number, label "mg/dL/min")
     - Signal loss: timeout Input (number, label "minutes")
   - Hysteresis: Input (number, label "minutes")
   - Confirmation readings: Input (number)
   - Sort order: Input (number)
   - Enabled: Switch

6. Save calls `createRule()` or `updateRule()` from the generated remote functions, serializing the form state back to the request DTO shape.

7. Run `pnpm run check` in the app package.

8. Commit: `feat: add RuleEditorSheet component (General tab)`

### Task 5.2: Presentation tab

**Files:**
- Modify: `src/Web/packages/app/src/lib/components/alerts/RuleEditorSheet.svelte`

**Steps:**

1. **Audio section:**
   - Enabled: Switch
   - Sound: Select with built-in presets (`alarm-default`, `alarm-urgent`, `alarm-high`, `alarm-low`, `alert`, `chime`, `bell`, `siren`, `beep`, `soft`) plus any custom sounds loaded from `GET /api/v4/alert-sounds`.
   - If custom sound selected, set `customSoundId` in the config.
   - Play preview: Button (creates `Audio` element, plays the sound — built-in from `/sounds/{name}.mp3`, custom from `/api/v4/alert-sounds/{id}/stream`). Disable if files don't exist yet.
   - Upload custom sound: file Input (accept `audio/*`), validate `file.size <= 512000` before upload. On success, refresh custom sounds list.
   - Ascending: Switch (reveals start volume slider when enabled)
   - Start volume: Slider (0-100, shown only when ascending)
   - Max volume: Slider (0-100)
   - Ascend duration: Input (seconds)
   - Repeat count: Input (number)

2. **Visual section:**
   - Flash enabled: Switch + color picker Input (type="color", shown when enabled)
   - Persistent banner: Switch
   - Wake screen: Switch

3. Run `pnpm run check`.

4. Commit: `feat: add Presentation tab to RuleEditorSheet`

### Task 5.3: Snooze tab

**Files:**
- Modify: `src/Web/packages/app/src/lib/components/alerts/RuleEditorSheet.svelte`

**Steps:**

1. Default snooze duration: Input (minutes)
2. Snooze options: chip list showing current options (e.g., "5m", "15m", "30m", "60m"). Each chip has an X to remove. Input + "Add" button to add new option.
3. Max snooze count: Input (number)
4. Smart snooze: Switch
5. Smart snooze extend minutes: Input (shown when smart snooze enabled)

6. Run `pnpm run check`.

7. Commit: `feat: add Snooze tab to RuleEditorSheet`

### Task 5.4: Schedules tab

**Files:**
- Modify: `src/Web/packages/app/src/lib/components/alerts/RuleEditorSheet.svelte`

**Steps:**

1. List of schedules. Each schedule is a collapsible section:
   - Name: Input
   - Default: Switch (exactly one must be default — if toggling on, turn off others)
   - Time window: start/end time Inputs (hidden for default schedule)
   - Days of week: 7 toggle buttons (Sun-Sat), null = all selected
   - Timezone: Input (text, default "UTC")
   - Escalation steps: vertical list
     - Each step: inline row showing "Step N" + delay Input (seconds) + channel chips
     - Each channel: type Select (web_push, webhook) + destination Input + label Input + remove button
     - "Add channel" button per step
     - Remove step button
   - "Add step" button
   - Remove schedule button (disabled if it's the only one / the default)

2. "Add schedule" button at the bottom.

3. Run `pnpm run check`.

4. Commit: `feat: add Schedules tab to RuleEditorSheet`

### Task 5.5: Wire the sheet into the alerts settings page

**Files:**
- Modify: `src/Web/packages/app/src/routes/settings/alerts/+page.svelte`

**Steps:**

1. Import `RuleEditorSheet`.

2. Add state:
   ```typescript
   let editorOpen = $state(false);
   let editingRule = $state<AlertRuleResponse | null>(null);
   ```

3. Add "Edit" button in the expanded rule actions area (next to Delete):
   ```svelte
   <Button variant="outline" size="sm" onclick={() => { editingRule = rule; editorOpen = true; }}>
     <Pencil class="h-4 w-4 mr-2" />
     Edit Rule
   </Button>
   ```

4. Change "Add Rule" button in the header to open the sheet in create mode:
   ```svelte
   <Button onclick={() => { editingRule = null; editorOpen = true; }}>
     <Plus class="h-4 w-4 mr-2" />
     Add Rule
   </Button>
   ```

5. Add the sheet at the bottom of the page:
   ```svelte
   <RuleEditorSheet bind:open={editorOpen} rule={editingRule} onSave={loadData} />
   ```

6. Run `pnpm run check`.

7. Commit: `feat: wire RuleEditorSheet into alerts settings page`

---

## Phase 6: Frontend — Quiet Hours Card

### Task 6.1: Add quiet hours card to alerts settings page

**Files:**
- Modify: `src/Web/packages/app/src/routes/settings/alerts/+page.svelte`

**Steps:**

1. Import the quiet hours remote functions (these will be generated after NSwag regen — `getQuietHours`, `updateQuietHours` from `$api/generated/alerts.generated.remote`).

2. Add state:
   ```typescript
   let quietHours = $state<QuietHoursResponse | null>(null);
   let quietHoursLoading = $state(false);
   ```

3. Load quiet hours in `loadData()`.

4. Add a Card below the rules list:
   - Switch: "Enable quiet hours" — toggles the section
   - When enabled: start time Input, end time Input
   - Switch: "Allow critical alerts during quiet hours" (default on)
   - Save button that calls `updateQuietHours()`

5. Run `pnpm run check`.

6. Commit: `feat: add quiet hours card to alerts settings page`

---

## Phase 7: Frontend — Update Setup Wizard

### Task 7.1: Update wizard presets with clientConfiguration and severity

**Files:**
- Modify: `src/Web/packages/app/src/routes/settings/alerts/setup/+page.svelte`

**Steps:**

1. Update each preset definition to include `severity` and `clientConfiguration` matching the design doc defaults table:

   - Urgent Low: severity "critical", alarm-urgent ascending 50-100%, flash red, persistent banner, wake screen, 5m snooze, smart snooze ON
   - Low: severity "normal", alarm-low ascending 30-80%, persistent banner, 15m snooze, smart snooze ON
   - High: severity "normal", alarm-high 60%, persistent banner, 30m snooze, smart snooze OFF
   - Urgent High: severity "critical", alarm-urgent ascending 50-100%, flash red, persistent banner, wake screen, 15m snooze, smart snooze OFF
   - Fast Drop: severity "normal", alert ascending 40-90%, persistent banner, 15m snooze, smart snooze ON
   - Sensor Lost: severity "normal", chime 50%, persistent banner, 30m snooze, smart snooze OFF

2. Include these fields in the `createRule()` calls when saving.

3. Run `pnpm run check`.

4. Commit: `feat: update setup wizard presets with severity and client configuration`

---

## Phase 8: Build, Test, Validate

### Task 8.1: Build and run tests

**Steps:**

1. `dotnet build -p:GenerateNSwagClient=false --verbosity quiet` — verify 0 compilation errors.
2. `dotnet test tests/Unit/Nocturne.API.Tests --filter "ConditionEvaluator|ExcursionTracker|ScheduleResolver" --no-build` — verify existing tests still pass.
3. Run `aspire run` to regenerate NSwag client with new DTOs.
4. Run `cd src/Web/packages/app && pnpm run check` — verify frontend types.
5. Fix any issues found.
6. Commit any fixes.

---

## Dependency Graph

```
Phase 1 (schema) → Phase 2 (update DTOs) → Phase 3 (new endpoints)
                                                      ↓
                                              Phase 4 (server behavior)
                                                      ↓
                              Phase 5 (rule editor sheet) + Phase 6 (quiet hours card)
                                                      ↓
                                              Phase 7 (wizard update)
                                                      ↓
                                              Phase 8 (validate)
```

Phase 5 tasks (5.1-5.5) are sequential — each tab builds on the sheet skeleton.
Phase 6 can run in parallel with Phase 5.
Phase 7 depends on the NSwag regen from Phase 8 step 3, but the code change is small.

# Alert Engine Design

**Date**: 2026-03-22
**Status**: Draft
**Scope**: Core alerting infrastructure for Nocturne — trigger evaluation, escalation chains, delivery dispatch, and acknowledgement lifecycle.

---

## Overview

Nocturne needs a centralised alert engine that evaluates glucose data against user-defined rules and routes notifications through escalation chains to multiple delivery channels. The engine is a core service — it is consumed by the web UI, the Chat SDK bot, Home Assistant webhooks, native webhooks, push notifications, and any future delivery channel. No delivery channel owns alerting logic; they are all dumb pipes that receive structured dispatch requests and render them for their platform.

The engine must be flexible enough for power users (time-of-day routing, multi-step escalation, composable trigger conditions) while remaining approachable for someone who just wants "tell me when I'm low." This is achieved through a single data model with progressive disclosure in the setup wizard — the simple case (one threshold, one group of recipients, no escalation) is just an escalation chain with one step.

---

## Core Concepts

### Alert Rule

A trigger condition paired with one or more time-based schedules. Each schedule contains an escalation chain.

A rule belongs to a tenant. A tenant may have many rules. Rules are independently evaluable — each tracks its own excursion lifecycle.

### Condition

The "if this" part of the rule. Conditions are composable: leaf conditions (threshold, rate of change, signal loss) can be combined with AND/OR operators into a condition tree. Conditions are stored as JSONB and evaluated by a registry of condition evaluators.

### Schedule

A time-of-day window that determines which escalation chain to use. Schedules are evaluated in the tenant's configured timezone. Every rule must have exactly one "default" schedule (no time constraint) that acts as the catch-all. Additional schedules override the default during their active windows.

Schedules support day-of-week constraints for cases like "school hours are Mon–Fri only."

**Schedule boundary behaviour**: When an excursion is active and a schedule boundary is crossed, the original schedule's escalation chain runs to completion. The schedule is determined at excursion creation time and does not change. The next excursion that starts after the boundary correctly uses the new schedule.

### Escalation Chain

An ordered sequence of escalation steps attached to a schedule. Each step specifies a set of delivery channels and fires after a delay, gated by acknowledgement. The purpose of escalation is to ramp up the annoyance factor until someone acknowledges — each step is another buzz, another person notified, another demand for attention.

### Escalation Step

A single stage in an escalation chain. Contains: a delay (0 for immediate), a list of delivery channel targets, and an implicit acknowledgement window (the time until the next step fires).

### Delivery Channel

A (type, destination) pair. Destinations can be either direct messages or group channels (e.g., a family Discord server channel or a Slack channel). Types include: `web_push`, `discord_dm`, `discord_channel`, `telegram_dm`, `telegram_group`, `whatsapp`, `slack_dm`, `slack_channel`, `webhook` (Nocturne's native webhook system), `homeassistant_webhook`, `email`. The alert engine does not know how to deliver to any of these — it writes a delivery request and the appropriate provider picks it up. Nocturne's native webhook delivery channel posts alert payloads to user-configured webhook URLs, enabling integration with any external system without a dedicated adapter.

### Excursion

A contiguous period where a rule's condition evaluates to true. Groups related alert instances together. Once acknowledged, the rule does not re-fire for the same excursion, but a _different_ rule breaching a new threshold (e.g., Low → Urgent Low) creates a new independent excursion.

### Alert Instance

A live occurrence linking a rule, an excursion, a schedule, and the current position in the escalation chain. Tracks state: `triggered → escalating → acknowledged | resolved`.

---

## Data Model

### alert_rules

```
alert_rules
├── id                    UUID PK
├── tenant_id             UUID FK → tenants
├── name                  TEXT ("Urgent Low", "Fast Drop", "Sensor Lost")
├── description           TEXT (optional, human-readable explanation)
├── condition_type        TEXT ("threshold" | "rate_of_change" | "signal_loss" | "composite")
├── condition_params      JSONB (see Condition Schema below)
├── hysteresis_minutes    INT (how long condition must be false before excursion closes)
├── confirmation_readings INT (consecutive true evaluations required before excursion opens)
├── is_enabled            BOOLEAN
├── sort_order            INT (evaluation priority / display order)
├── created_at            TIMESTAMPTZ
├── updated_at            TIMESTAMPTZ
```

### alert_schedules

```
alert_schedules
├── id                    UUID PK
├── alert_rule_id         UUID FK → alert_rules
├── name                  TEXT ("Default", "School Hours", "Night")
├── is_default            BOOLEAN (exactly one per rule must be true)
├── days_of_week          INT[] (0=Sun..6=Sat, null = all days)
├── start_time            TIME (null for default schedule)
├── end_time              TIME (null for default schedule)
├── timezone              TEXT (IANA timezone, denormalised from tenant for query convenience)
├── created_at            TIMESTAMPTZ
├── updated_at            TIMESTAMPTZ
```

**Constraint**: Exactly one schedule per rule must have `is_default = true` with null `start_time`/`end_time`.

**Constraint**: Non-default schedules must not overlap in time for the same rule.

### alert_escalation_steps

```
alert_escalation_steps
├── id                    UUID PK
├── alert_schedule_id     UUID FK → alert_schedules
├── step_order            INT (0-indexed, 0 = immediate)
├── delay_seconds         INT (0 for the first step, positive for subsequent)
├── created_at            TIMESTAMPTZ
```

### alert_step_channels

```
alert_step_channels
├── id                    UUID PK
├── escalation_step_id    UUID FK → alert_escalation_steps
├── channel_type          TEXT ("web_push" | "discord_dm" | "discord_channel" | "telegram_dm" | "telegram_group" | "slack_dm" | "slack_channel" | "whatsapp" | "webhook" | ...)
├── destination           TEXT (platform-specific address)
├── destination_label     TEXT ("Mum's WhatsApp", "School Nurse", for display)
├── created_at            TIMESTAMPTZ
```

### alert_tracker_state

Persists the ExcursionTracker state machine to survive process restarts. One row per rule.

```
alert_tracker_state
├── alert_rule_id         UUID PK (one row per rule)
├── tenant_id             UUID FK → tenants
├── state                 TEXT ("idle" | "confirming" | "active" | "hysteresis")
├── confirmation_count    INT (readings counted so far in confirming state)
├── active_excursion_id   UUID FK → alert_excursions (null when idle/confirming)
├── updated_at            TIMESTAMPTZ
```

### alert_excursions

```
alert_excursions
├── id                    UUID PK
├── alert_rule_id         UUID FK → alert_rules
├── tenant_id             UUID FK → tenants
├── started_at            TIMESTAMPTZ
├── ended_at              TIMESTAMPTZ (null while active)
├── acknowledged_at       TIMESTAMPTZ (null if not acknowledged)
├── acknowledged_by       TEXT (identifier of person who acked)
├── hysteresis_started_at TIMESTAMPTZ (null if condition is still true)
```

### alert_instances

```
alert_instances
├── id                    UUID PK
├── alert_excursion_id    UUID FK → alert_excursions
├── alert_schedule_id     UUID FK → alert_schedules (set once at creation, never changes)
├── current_step_order    INT
├── status                TEXT ("triggered" | "escalating" | "acknowledged" | "resolved")
├── triggered_at          TIMESTAMPTZ
├── resolved_at           TIMESTAMPTZ
├── next_escalation_at    TIMESTAMPTZ (when the next step should fire, null if fully escalated or acked)
```

### alert_deliveries

```
alert_deliveries
├── id                    UUID PK
├── alert_instance_id     UUID FK → alert_instances
├── escalation_step_id    UUID FK → alert_escalation_steps
├── channel_type          TEXT
├── destination           TEXT
├── payload               JSONB (structured alert data — see Alert Payload section)
├── status                TEXT ("pending" | "delivered" | "failed" | "expired")
├── platform_message_id   TEXT (returned after delivery, for message editing)
├── platform_thread_id    TEXT (for platforms that need both)
├── created_at            TIMESTAMPTZ
├── delivered_at          TIMESTAMPTZ
├── retry_count           INT DEFAULT 0
├── last_error            TEXT
├── tenant_id             UUID FK → tenants
```

### alert_invites

```
alert_invites
├── id                    UUID PK
├── tenant_id             UUID FK → tenants
├── created_by            UUID FK → users (the person who generated the invite)
├── token                 TEXT UNIQUE (the secret in the invite URL)
├── escalation_step_id    UUID FK → alert_escalation_steps
├── permission_scope      TEXT ("view_only" | "view_acknowledge" | "full_caregiver")
├── is_used               BOOLEAN DEFAULT false
├── used_by               UUID FK → users (null until redeemed)
├── expires_at            TIMESTAMPTZ (default: 7 days from creation)
├── created_at            TIMESTAMPTZ
```

### Denormalised Fields

**tenants.last_reading_at**: Updated on every glucose reading ingest. Used by the signal loss sweep to avoid querying the entries table.

---

## Condition Schema

All conditions are stored as JSONB in `condition_params`. The `condition_type` discriminator on the rule tells the engine which evaluator to use.

**Canonical unit**: All glucose values are stored and evaluated in **mg/dL**. This matches Nocturne's internal convention — the frontend is responsible for translating to mmol/L for display. The setup wizard and settings UI accept values in the user's preferred unit and convert to mg/dL before storing.

### Leaf Conditions

**Threshold:**

```json
{
  "direction": "below",
  "value": 70
}
```

**Rate of Change:**

```json
{
  "direction": "falling",
  "rate": 3.0
}
```

Values are in mg/dL and mg/dL/min respectively. No unit field is needed — it is always mg/dL.

**Rate of change data source**: The evaluator uses a provider hierarchy: AID system calculated rate → CGM trend data → server-calculated fallback from recent readings. This follows the same provider pattern established for IOB/COB — the AID system's calculations are authoritative when fresh.

**Signal Loss:**

```json
{
  "timeout_minutes": 15
}
```

### Composite Conditions

```json
{
  "operator": "and",
  "conditions": [
    { "type": "threshold", "direction": "below", "value": 100 },
    { "type": "rate_of_change", "direction": "falling", "rate": 3.0 }
  ]
}
```

Composite conditions use `condition_type = "composite"` on the rule. The `operator` field supports `"and"` and `"or"`. Nesting is supported — a composite can contain other composites.

### Evaluator Registry

Each condition type has a corresponding evaluator that implements a single interface:

```csharp
public interface IConditionEvaluator
{
    bool Evaluate(ConditionParams condition, SensorContext context);
}
```

`SensorContext` contains the data evaluators need: latest reading(s), timestamps, trend data, AID system rate of change. It does not contain alert state — evaluators are pure functions of sensor data.

A `CompositeEvaluator` recurses over child conditions, applies the AND/OR operator, and returns a single boolean. The engine never sees the tree — it calls one evaluator and gets one boolean.

Adding a new condition type requires: writing an evaluator, registering it in the evaluator registry. No schema changes, no engine changes.

---

## Alert Payload

The alert engine produces a structured JSON payload for each delivery. Providers are responsible for rendering this into platform-appropriate format (cards, messages, webhook bodies) including unit conversion to the recipient's preferred display unit.

```json
{
  "alert_type": "threshold",
  "rule_name": "Urgent Low",
  "severity": "urgent",
  "glucose_value": 56,
  "trend": "falling",
  "trend_rate": -3.2,
  "reading_timestamp": "2026-03-22T14:32:00Z",
  "excursion_id": "...",
  "instance_id": "...",
  "tenant_id": "...",
  "subject_name": "Alex",
  "active_excursion_count": 2
}
```

The `subject_name` identifies whose BG this is (critical for caregivers watching multiple people). The `active_excursion_count` tells providers how many concurrent situations are active for context.

---

## Excursion Tracking

The **ExcursionTracker** wraps the evaluator output and manages the lifecycle of excursions. It is a generic piece of infrastructure that applies identically to all condition types.

### State Transitions

On each evaluation (triggered by new data or by the periodic sweep):

1. Run the condition evaluator → `boolean`
2. Feed the boolean into the ExcursionTracker for that rule

The tracker maintains the following state machine:

```
                      ┌─────────────────────────────────┐
                      │                                 │
  ┌──────┐    true    │  ┌────────────┐    confirmed    │  ┌──────────┐
  │ IDLE │───────────►│  │ CONFIRMING │───────────────►│  │ ACTIVE   │
  │      │            │  │            │                │  │          │
  └──────┘            │  └────────────┘                │  └──────────┘
     ▲                │       │ false                   │       │ false
     │                │       ▼                         │       ▼
     │                │  back to IDLE                   │  ┌──────────────┐
     │                │                                 │  │ HYSTERESIS   │
     │                └─────────────────────────────────┘  │              │
     │                                                     └──────────────┘
     │                                                          │
     │                    hysteresis expired                     │
     │◄─────────────────────────────────────────────────────────┘
     │
     │                    true during hysteresis → back to ACTIVE
```

**IDLE → CONFIRMING**: First `true` evaluation. Counter starts.

**CONFIRMING → ACTIVE**: `confirmation_readings` consecutive `true` evaluations reached. Excursion record created. Alert instance created. Escalation chain begins.

**CONFIRMING → IDLE**: A `false` evaluation before confirmation threshold. Reset counter.

**ACTIVE → HYSTERESIS**: First `false` evaluation. `hysteresis_started_at` recorded.

**HYSTERESIS → ACTIVE**: A `true` evaluation before hysteresis expires. Resume excursion. Previous acknowledgement still applies — no new alert.

**HYSTERESIS → IDLE**: `hysteresis_minutes` elapsed with sustained `false`. Excursion closed (`ended_at` set). Rule re-armed.

All tracker state is persisted in the `alert_tracker_state` table to survive process restarts. The CONFIRMING state's counter is preserved — a restart mid-confirmation does not reset progress.

### Confirmation Window

The `confirmation_readings` parameter prevents single errant readings (compression lows, brief signal glitches) from firing alerts. A value of 2 means two consecutive readings must satisfy the condition. At typical 5-minute CGM intervals, this is a 10-minute confirmation window.

### Hysteresis

The `hysteresis_minutes` parameter prevents flapping. BG oscillating around a threshold (68, 72, 67, 74 mg/dL) causes the evaluator to alternate between true and false, but the excursion stays open during the hysteresis window. Only a sustained return to range closes the excursion and re-arms the rule.

### Concurrency

Both the event-driven loop and the periodic sweep may attempt to update the same excursion simultaneously. Concurrency is handled via `SELECT ... FOR UPDATE` on the `alert_excursions` row. The losing operation blocks briefly, reads the updated state, and either finds the work already done or proceeds with the current state.

---

## Evaluation Loops

The engine has two evaluation mechanisms that feed into the same ExcursionTracker and escalation machinery.

### Event-Driven Loop

Triggered by each new glucose reading arriving for a tenant. Evaluates threshold and rate-of-change conditions. This is the fast path — a new reading fires within seconds.

### Periodic Sweep

A background service running every 30 seconds. Performs three focused operations:

```csharp
public class AlertSweepService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await AdvanceEscalations(ct);
            await CloseHysteresisWindows(ct);
            await EvaluateSignalLoss(ct);

            await Task.Delay(TimeSpan.FromSeconds(30), ct);
        }
    }
}
```

**Escalation advancement**: `SELECT * FROM alert_instances WHERE status = 'escalating' AND next_escalation_at <= now()`. Only touches active, unacknowledged instances — a tiny result set regardless of tenant count. Index on `(status, next_escalation_at)`.

**Hysteresis closure**: `SELECT * FROM alert_excursions WHERE ended_at IS NULL AND hysteresis_started_at IS NOT NULL AND hysteresis_started_at + hysteresis_minutes <= now()`. Also bounded by active excursions, not total tenants.

**Signal loss**: Checks `tenants.last_reading_at` against signal loss rules. This is the only query that scales with tenant count, but it's a single indexed query: `SELECT tenant_id FROM tenants WHERE last_reading_at < now() - interval AND has_signal_loss_rules`. At 10,000 tenants, this runs in under a millisecond.

The sweep is critical because it handles scenarios where no new data is arriving — signal loss by definition, but also escalation advancement during data gaps (if the CGM disconnects during an active low excursion, escalation must continue, not stall).

---

## Evaluation Loop Detail

When a new glucose reading arrives for a tenant (event-driven) or the sweep identifies a tenant needing evaluation:

1. Load all enabled rules for the tenant.
2. For each rule:
   a. Acquire `SELECT ... FOR UPDATE` on the `alert_tracker_state` row for this rule.
   b. Determine the active schedule (evaluate time-of-day windows in tenant timezone; fall back to default).
   c. Run the condition evaluator with the current sensor context → `boolean`.
   d. Feed the boolean to the ExcursionTracker → state transition.
   e. If transition is **excursion opened**: create excursion and alert instance records (schedule is set once, never changes). Write delivery requests for Step 0 of the active schedule's escalation chain. Set `next_escalation_at` for Step 1.
   f. If transition is **excursion continues**: check if `next_escalation_at` has passed and the instance is not acknowledged. If so, advance to the next step, write delivery requests, update `next_escalation_at`.
   g. If transition is **excursion closed**: set `ended_at`, resolve alert instance, cancel any pending delivery requests. Broadcast `alert_resolved` event so providers can update cards.
   h. Persist tracker state to `alert_tracker_state`.

---

## Acknowledgement

Acknowledging an alert means **"I am aware of everything happening with this person right now."** Tapping Acknowledge on any card for a tenant acknowledges ALL active excursions for that tenant. There is no single-alert acknowledgement — from the recipient's perspective, the child going low is one situation regardless of how many rules have fired.

When an acknowledgement arrives via `POST /api/v4/alerts/tenants/{tenantId}/acknowledge`:

1. Set `acknowledged_at` and `acknowledged_by` on ALL active excursions for that tenant.
2. Set status to `acknowledged` on all corresponding alert instances.
3. Clear `next_escalation_at` on all instances — no further escalation steps fire.
4. **Do not close excursions.** They remain open until hysteresis logic closes them. This means the same rules won't re-fire while conditions persist.
5. Broadcast an `alert_acknowledged` event so delivery providers can update ALL previously sent messages for this tenant to show "Acknowledged by {name}" and disable buttons.

### Card Lifecycle via Message Editing

Delivered alert cards are updated in place as the situation evolves. A card may be edited multiple times over its lifecycle:

1. **Initial delivery**: "⚠️ Urgent Low — Alex is 52 mg/dL and falling"
2. **Acknowledged**: Card edited to show "Acknowledged by Mum" — button disabled
3. **Resolved**: Card edited to show "Resolved — back in range (120 mg/dL)"

Platforms that don't support message editing (WhatsApp) receive follow-up messages for acknowledgement events. Resolution updates are skipped for non-editable platforms to avoid unnecessary noise — the acknowledgement is the important signal.

---

## Delivery Dispatch

### Dual-Channel Delivery

**Fast path (SignalR)**: When delivery requests are written, the engine publishes an `alert_dispatch` event on the SignalR hub. Connected delivery providers (the Chat SDK bot, the web UI) receive it and deliver immediately.

**Reliability path (database)**: All delivery requests are persisted in `alert_deliveries` with status `pending`. Providers call back to mark deliveries as `delivered`. A background sweep picks up deliveries that have been `pending` for longer than 10 seconds and redelivers them. This handles provider restarts, transient platform outages, and race conditions.

### Delivery Provider Heartbeat

Delivery providers send periodic heartbeats to `POST /api/v4/system/heartbeat` with their active channel types. If Nocturne hasn't received a heartbeat from a provider within 90 seconds, those channel types are marked as `degraded`. The engine skips degraded channels when dispatching, and advances to the next escalation step rather than waiting for the acknowledgement window to expire.

The web UI surfaces degraded channels: "Discord, Telegram, WhatsApp, and Slack alerts are currently unavailable."

---

## Setup Wizard

The setup wizard guides first-time users through alert configuration without exposing the underlying complexity. It presents preset alert types that map to pre-built condition trees:

- **Low glucose**: `threshold(below, 70)` — confirmation: 2, hysteresis: 15 min
- **Urgent low**: `threshold(below, 54)` — confirmation: 1, hysteresis: 15 min
- **High glucose**: `threshold(above, 250)` — confirmation: 3, hysteresis: 30 min
- **Urgent high**: `threshold(above, 300)` — confirmation: 2, hysteresis: 30 min
- **Fast drop**: `and(threshold(below, 100), rate_of_change(falling, 3.0))` — confirmation: 2, hysteresis: 15 min
- **Sensor lost**: `signal_loss(15 min)` — confirmation: 1, hysteresis: 5 min

All threshold values are stored in mg/dL. The wizard presents values in the user's preferred unit and converts on save.

The wizard allows users to adjust threshold values and select delivery channels. It creates a single default schedule (no time-of-day routing). Users who want time-based schedules or custom composite conditions access these through the full settings UI.

### Invite Links for Escalation Steps

When configuring escalation steps that include other people (partner, grandparent, school nurse), the wizard generates **single-use, time-limited invite links** per step.

The invite link encodes: tenant ID, escalation step, and permission scope. When the recipient clicks it, the flow is:

1. No Nocturne account → sign up (minimal: name, email, password)
2. Already have an account → log in
3. Automatically granted scoped caregiver access to the tenant (permission level chosen by the inviter: view only, view + acknowledge, or full caregiver)
4. Pick a chat platform (Discord / Telegram / WhatsApp / Slack)
5. Authorize the bot on that platform
6. Placed into the correct escalation step

The recipient never sees the word "escalation." Their experience is: "I clicked a link, signed up, picked WhatsApp, and now I get messages if Alex goes low."

Invite link properties:

- **Single-use**: One invite, one recipient. Prevents uncontrolled sharing.
- **Time-limited**: 7-day expiry by default. Regenerable by the inviter.
- **Permission-scoped**: The inviter configures the access level: `view_only` (see glucose, receive alerts), `view_acknowledge` (also acknowledge alerts), or `full_caregiver` (full read access to all tenant data).
- **Visible status**: The wizard and settings UI show pending/redeemed state per invite: "Step 2: Grandma — Pending, School Nurse — Connected (WhatsApp) ✓"

Escalation steps with only pending (unconnected) recipients are skipped by the engine, which advances to the next step.

### Medical Disclaimer

The wizard includes a medical disclaimer: "Nocturne alerts are provided as a convenience and may be delayed or fail due to network issues, platform outages, or system errors. They are not a substitute for your CGM's built-in alarms or medical device alerts. Do not rely on Nocturne as your sole alerting system."

---

## Future Considerations (Out of Scope for V1)

- **Prediction-based alerts**: Using AID system predicted BG values (30–60 min forecast) as trigger inputs. The evaluator registry is extensible for this — a `PredictionEvaluator` would be a new leaf condition type.
- **Context-dependent thresholds**: Adjusting rule parameters based on AID system state (open loop, stale sensor). Could be modelled as an additional schedule dimension or as a condition modifier.
- **Rate limiting / digest mode**: Bundling multiple non-urgent alerts into a periodic summary instead of individual notifications.
- **Alert analytics**: Tracking alert frequency, false positive rates, and acknowledgement response times to help users tune their rules.

---

## Resolved Decisions

1. **Group channels**: Escalation steps support posting to shared channels (e.g., a family Discord server channel, a Slack channel) in addition to DMs. Channel types are split accordingly: `discord_dm` vs `discord_channel`, `slack_dm` vs `slack_channel`, `telegram_dm` vs `telegram_group`.
2. **Unit handling**: All glucose values are stored and evaluated in mg/dL (Nocturne's canonical internal unit). Frontend and setup wizard translate to/from mmol/L for display.
3. **Native webhooks**: Nocturne's existing webhook system is a first-class delivery channel type (`webhook`), enabling integration with any external system.
4. **API version**: All endpoints use v4 (`/api/v4/...`). Versions v1–v3 are reserved exclusively for Nightscout compatibility.
5. **Evaluator purity**: Condition evaluators are pure functions of sensor data. The ExcursionTracker wraps the boolean output and manages lifecycle, hysteresis, and confirmation independently of condition type.
6. **Schedule boundaries**: The original schedule's escalation chain runs to completion. No mid-excursion schedule switching.
7. **Acknowledgement scope**: Acknowledge always means "all active excursions for this tenant." No single-alert ack. No severity groups or cascading.
8. **Card lifecycle**: Delivered cards are edited in place: Active → Acknowledged → Resolved. No new notification on resolution.
9. **Signal loss trigger**: Handled by the periodic sweep (not the event-driven loop), since signal loss means no data is arriving.
10. **Escalation timer advancement**: The periodic sweep advances escalation timers, ensuring escalation continues even during data gaps.
11. **Concurrency**: Row-level locking (`SELECT ... FOR UPDATE`) on the excursion row to serialise the event-driven and sweep loops.
12. **Tracker state persistence**: The `alert_tracker_state` table persists the ExcursionTracker state machine (including CONFIRMING counter) to survive process restarts.
13. **Sweep performance**: Three focused queries (escalation, hysteresis, signal loss) with `tenants.last_reading_at` denormalisation for signal loss.
14. **Alert payload**: Structured data (not pre-rendered text). Providers handle unit conversion and platform-specific rendering.
15. **Rate of change source**: Provider hierarchy — AID system rate → CGM trend → calculated fallback.
16. **Invite links**: Single-use, 7-day expiry, permission-scoped invite links for onboarding new users (including account creation) into escalation steps.

---

## Open Questions

1. **Escalation chain for web UI**: The web UI is always-on and doesn't need escalation in the traditional sense. Should it be treated as a delivery channel in escalation steps, or should it receive all alerts for a tenant independently of the escalation chain?

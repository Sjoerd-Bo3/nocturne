# Alert Rule Editor, Client Config, and Quiet Hours Design

**Date**: 2026-03-22
**Status**: Approved
**Scope**: Rule editor sheet UI, per-rule client presentation config (audio/visual/snooze), smart snooze, custom sound upload, quiet hours, and setup wizard preset defaults.

---

## Overview

The alert engine has rules, schedules, escalation chains, and delivery — but no way to edit them from the frontend beyond the setup wizard presets. This design adds a full rule editor, per-rule client-side presentation config, custom sound upload, smart snooze, and tenant-level quiet hours.

---

## Schema Changes

### AlertRuleEntity — add fields

- `client_configuration` (JSONB, default "{}") — audio, visual, snooze settings consumed by the frontend
- `severity` (string, max 16, default "normal") — `"normal"` | `"critical"`. Only server-side use: critical alerts bypass quiet hours.

### AlertInstanceEntity — add fields

- `snoozed_until` (DateTime?) — when the snooze expires, null if not snoozed
- `snooze_count` (int, default 0) — how many times this instance has been snoozed

### TenantEntity — add fields

- `quiet_hours_start` (TimeOnly?) — null means quiet hours disabled
- `quiet_hours_end` (TimeOnly?)
- `quiet_hours_override_critical` (bool, default true) — critical severity alerts bypass quiet hours

### New table: alert_custom_sounds

- `id` (UUID PK)
- `tenant_id` (UUID FK, ITenantScoped)
- `name` (string, max 128)
- `mime_type` (string, max 64)
- `data` (bytea — raw audio bytes)
- `file_size` (int)
- `created_at` (DateTime)

Constraint: max 500KB per file, enforced frontend + backend.

---

## client_configuration JSON Shape

```json
{
  "audio": {
    "enabled": true,
    "sound": "alarm-urgent",
    "customSoundId": null,
    "ascending": true,
    "startVolume": 30,
    "maxVolume": 100,
    "ascendDurationSeconds": 30,
    "repeatCount": 3
  },
  "visual": {
    "flashEnabled": false,
    "flashColor": "#ff0000",
    "persistentBanner": true,
    "wakeScreen": true
  },
  "snooze": {
    "defaultMinutes": 15,
    "options": [5, 15, 30, 60],
    "maxCount": 5,
    "smartSnooze": true,
    "smartSnoozeExtendMinutes": 10
  }
}
```

This config is **client-side only**. The server stores it and includes it in alert payloads. The web UI reads it to decide how to present alerts (which sound, volume behavior, snooze options). The server never makes decisions based on audio/visual settings.

The server uses `severity` (on the rule entity, NOT in client config) for quiet hours bypass decisions.

---

## API Changes

### Alert Rules Controller — update DTOs

- `AlertRuleResponse` gains `clientConfiguration` (object) and `severity` (string)
- `CreateAlertRuleRequest` gains `clientConfiguration` (object?) and `severity` (string?)
- `UpdateAlertRuleRequest` same

### New: Custom Sounds Controller

Route: `api/v4/alert-sounds`

- `POST /` — upload a sound (multipart form, 500KB max). Returns `AlertCustomSoundResponse`.
- `GET /` — list all custom sounds for the tenant.
- `GET /{id}/stream` — stream raw audio with correct Content-Type and `Cache-Control: max-age=86400`.
- `DELETE /{id}` — delete a custom sound.

### New: Quiet Hours endpoints on AlertsController

- `GET /quiet-hours` — returns current quiet hours config for the tenant.
- `PUT /quiet-hours` — update quiet hours config.

### New: Snooze endpoint on AlertsController

- `POST /instances/{instanceId}/snooze` — body: `{ "minutes": 15 }`.
  - 400 if minutes not in the rule's `snooze.options` array.
  - 409 if `snooze_count >= snooze.maxCount`.
  - Sets `snoozed_until` and increments `snooze_count`.

---

## Server-Side Behavior Changes

### Quiet Hours

`AlertDeliveryService` checks before dispatching:
1. Is quiet hours enabled for this tenant?
2. Is the current time within the quiet hours window (in tenant timezone)?
3. If yes: is the rule's severity `"critical"` AND `quiet_hours_override_critical` is true?
4. If critical override applies, dispatch anyway. Otherwise, skip dispatch.

### Smart Snooze

`AlertSweepService` checks snoozed instances on each tick:
1. Is `snoozed_until` in the past?
2. Load the rule's `client_configuration.snooze`.
3. If `smartSnooze` is enabled AND `snooze_count < maxCount`:
   a. Check glucose trend — is it favorable? (For low alerts: rising. For high alerts: falling.)
   b. If favorable, extend: `snoozed_until += smartSnoozeExtendMinutes`, increment `snooze_count`.
   c. If not favorable, clear snooze — alert re-fires (resume escalation).
4. If smart snooze disabled or max count reached, clear snooze — alert re-fires.

### Snooze and Escalation

When an instance is snoozed, the sweep skips escalation advancement for that instance. `next_escalation_at` is not cleared — it's just deferred. When snooze expires (or smart snooze declines to extend), escalation resumes from where it left off.

---

## Frontend: Rule Editor Sheet

Triggered by "Edit" on expanded rule card or "Add Rule" button (create mode).

### Tab: General

- Name (text input)
- Description (text input)
- Severity toggle (normal / critical)
- Condition type selector (threshold / rate_of_change / signal_loss) — composite shown as read-only summary, editing deferred
- Condition params (dynamic based on type):
  - Threshold: direction dropdown (below/above) + value input (mg/dL)
  - Rate of change: direction dropdown (falling/rising) + rate input (mg/dL/min)
  - Signal loss: timeout input (minutes)
- Hysteresis (minutes input)
- Confirmation readings (number input)
- Sort order (number input)
- Enabled toggle

### Tab: Presentation

- **Audio section:**
  - Enabled toggle
  - Sound selector (built-in presets + custom sounds from tenant)
  - Play preview button
  - Upload custom sound button (file picker, 500KB limit, audio/* accept)
  - Ascending volume toggle
  - Start volume slider (shown when ascending enabled)
  - Max volume slider
  - Ascend duration (seconds input)
  - Repeat count (number input)

- **Visual section:**
  - Screen flash toggle + color picker
  - Persistent banner toggle
  - Wake screen toggle

### Tab: Snooze

- Default snooze duration (minutes input)
- Snooze options (chip list with add/remove — e.g., 5, 15, 30, 60)
- Max snooze count (number input)
- Smart snooze toggle
- Smart snooze extend minutes (shown when smart snooze enabled)

### Tab: Schedules

- List of schedules, each expandable:
  - Name, default toggle
  - Time window (start/end time pickers, hidden for default schedule)
  - Days of week (toggle buttons for each day)
  - Timezone selector
  - Escalation steps as vertical timeline:
    - Step N: delay input + channel list
    - Each channel: type selector + destination input + label
    - "Add channel" button
  - "Add step" button
- "Add schedule" button

### Save

Calls `updateRule()` or `createRule()` with the full payload including `clientConfiguration` and `severity`.

---

## Frontend: Quiet Hours Card

On the alerts settings page, below rules list, above history:

- Enable toggle (controls whether start/end times are shown)
- Start time picker
- End time picker
- "Allow critical alerts during quiet hours" toggle (defaults on)

Calls `PUT /api/v4/alerts/quiet-hours` on save.

---

## Setup Wizard Preset Defaults

| Preset | Severity | Audio | Visual | Snooze |
|--------|----------|-------|--------|--------|
| Urgent Low | critical | alarm-urgent, ascending 50-100%, 3 repeats | flash red, persistent banner, wake screen | 5m default, smart snooze ON, extend 10m |
| Low | normal | alarm-low, ascending 30-80%, 2 repeats | persistent banner | 15m default, smart snooze ON, extend 10m |
| High | normal | alarm-high, 60% volume, 2 repeats | persistent banner | 30m default, smart snooze OFF |
| Urgent High | critical | alarm-urgent, ascending 50-100%, 3 repeats | flash red, persistent banner, wake screen | 15m default, smart snooze OFF |
| Fast Drop | normal | alert, ascending 40-90%, 2 repeats | persistent banner | 15m default, smart snooze ON, extend 10m |
| Sensor Lost | normal | chime, 50% volume, 1 repeat | persistent banner | 30m default, smart snooze OFF |

---

## Out of Scope

- Composite condition editor (shown read-only)
- Actual audio files (preset names stubbed, files sourced later)
- Custom vibration patterns
- Forecast alerts
- Emergency contacts
- Per-field config inheritance/overrides

---

## Resolved Decisions

1. `client_configuration` is per-rule, client-side only. Server never reads audio/visual settings.
2. `severity` is a separate field on the rule entity, used server-side for quiet hours bypass.
3. Quiet hours is tenant-level, single window, with critical override.
4. Snooze is server-side (`snoozed_until` on instance), pauses escalation.
5. Smart snooze checked by sweep, extends if trend favorable, respects maxCount.
6. Custom sounds stored as bytea in PostgreSQL, 500KB limit, streamed via API with browser caching.
7. Snooze endpoint validates duration against rule options (400) and max count (409).
8. Setup wizard updated to populate `clientConfiguration` and `severity` on created rules.
9. Built-in sound names referenced by string, actual audio files sourced separately.
10. Composite conditions shown read-only in the editor, editing deferred.

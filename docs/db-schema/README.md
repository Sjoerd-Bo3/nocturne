# Nocturne Database Schema

> Entity-relationship reference for the Nocturne PostgreSQL database, generated from the EF Core model snapshot on the upstream `nightscout/nocturne@main` branch (commit `e7937ef`, 2026-06-25).

**[Open `schema.html`](./schema.html)** for the interactive explorer (search, pan/zoom, per-table column &amp; relationship detail) &mdash; a single self-contained file you can open in any browser or share directly.

**93 tables**, **1227 columns**, **149 foreign-key relationships**, grouped into **12 functional domains**.

All tenant-scoped tables carry a `tenant_id` column and are protected by PostgreSQL Row Level Security (`FORCE ROW LEVEL SECURITY`). Tables use snake_case names; new rows use UUID v7 primary keys. Timestamps are `timestamp with time zone`.

## Contents

- [Domain map](#domain-map)
- [Tenancy & Membership](#tenancy--membership)
- [Identity & Authentication](#identity--authentication)
- [OAuth Server](#oauth-server)
- [Glucose & Vitals (v4)](#glucose--vitals-v4)
- [Insulin & Therapy (v4)](#insulin--therapy-v4)
- [Devices & Status Snapshots (v4)](#devices--status-snapshots-v4)
- [Food](#food)
- [Alerts](#alerts)
- [Trackers](#trackers)
- [Connectors & Migration](#connectors--migration)
- [Audit & Event Logs](#audit--event-logs)
- [Platform & Misc](#platform--misc)

The complete single-diagram source (all 93 tables in one ER diagram) is in [`schema.full.mmd`](./schema.full.mmd).

## Domain map

High-level view of how the domains reference one another (arrows point from the domain holding the foreign key to the domain it references; numbers are distinct FK paths).

```mermaid
flowchart LR
  TM["Tenancy & Membership"]
  IA["Identity & Authentication"]
  OS["OAuth Server"]
  GVV["Glucose & Vitals (v4)"]
  ITV["Insulin & Therapy (v4)"]
  DSSV["Devices & Status Snapshots (v4)"]
  F["Food"]
  A["Alerts"]
  T["Trackers"]
  CM["Connectors & Migration"]
  AEL["Audit & Event Logs"]
  PM["Platform & Misc"]
  ITV -->|11| TM
  A -->|9| TM
  DSSV -->|9| TM
  ITV -->|9| CM
  GVV -->|8| TM
  CM -->|7| TM
  ITV -->|6| DSSV
  OS -->|5| TM
  T -->|5| TM
  PM -->|4| TM
  F -->|4| TM
  GVV -->|4| CM
  AEL -->|3| TM
  OS -->|3| IA
  TM -->|2| IA
  DSSV -->|2| CM
  F -->|1| ITV
  GVV -->|1| DSSV
```

## Tenancy & Membership

Tables: `tenants`, `tenant_members`, `tenant_member_roles`, `tenant_roles`, `member_invites`, `membership_requests`, `tenant_alert_settings`, `tenant_audit_config`, `tenant_data_retention_config`, `tenant_demo_config`, `settings`, `platform_settings`

```mermaid
erDiagram
  "tenants" {
    uuid id PK
    bool allow_access_requests
    varchar256 display_name
    bool is_active
    bool is_demo
    timestamptz last_reading_at
    timestamptz onboarding_completed_at
    timestamptz share_last_accessed_at
    varchar32 share_token
    timestamptz share_token_set_at
    varchar64 slug
    timestamptz sys_created_at
    timestamptz sys_updated_at
  }
  "tenant_members" {
    uuid id PK
    uuid created_from_invite_id FK
    varchar255 label
    timestamptz last_used_at
    varchar45 last_used_ip
    text last_used_user_agent
    bool limit_to_24_hours
    timestamptz revoked_at
    uuid subject_id FK
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    varchar50 username
  }
  "tenant_member_roles" {
    uuid id PK
    timestamptz sys_created_at
    uuid tenant_member_id FK
    uuid tenant_role_id FK
  }
  "tenant_roles" {
    uuid id PK
    varchar500 description
    bool is_system
    varchar100 name
    varchar100 slug
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "member_invites" {
    uuid id PK
    timestamptz created_at
    uuid created_by_subject_id FK
    timestamptz expires_at
    varchar255 label
    bool limit_to_24_hours
    int max_uses
    timestamptz revoked_at
    uuid tenant_id FK
    varchar64 token_hash
    int use_count
  }
  "membership_requests" {
    uuid id PK
    timestamptz created_at
    timestamptz decided_at
    uuid decided_by_subject_id
    varchar500 message
    varchar20 status
    uuid subject_id
    uuid tenant_id FK
  }
  "tenant_alert_settings" {
    uuid id PK
    timestamptz created_at
    bool dnd_manual_active
    timestamptz dnd_manual_started_at
    timestamptz dnd_manual_until
    bool dnd_schedule_enabled
    time_without_time_zone dnd_schedule_end
    time_without_time_zone dnd_schedule_start
    uuid tenant_id FK
    timestamptz updated_at
  }
  "tenant_audit_config" {
    uuid id PK
    int mutation_audit_retention_days
    bool read_audit_enabled
    int read_audit_retention_days
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "tenant_data_retention_config" {
    uuid id PK
    timestamptz created_at
    int soft_delete_retention_days
    uuid tenant_id FK
    timestamptz updated_at
  }
  "tenant_demo_config" {
    uuid tenant_id PK,FK
    varchar32 access_mode
    int backfill_days
    int interval_minutes
    timestamptz last_reset_at
    timestamptz next_reset_at
    int reset_interval_minutes
  }
  "settings" {
    uuid id PK
    jsonb additional_properties
    varchar200 app
    varchar50 created_at
    varchar200 device
    varchar200 entered_by
    bool is_active
    varchar500 key
    bigint mills
    varchar1000 notes
    varchar24 original_id
    timestamptz srv_created
    timestamptz srv_modified
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    int utc_offset
    text value
    int version
  }
  "platform_settings" {
    uuid id PK
    varchar100 category
    bool enabled
    jsonb encrypted_json
    timestamptz sys_created_at
    timestamptz sys_updated_at
  }
  "tenants" ||--o{ "member_invites" : "TenantId"
  "tenants" ||--o{ "membership_requests" : "TenantId"
  "tenants" ||--o{ "settings" : "TenantId"
  "tenants" ||--o{ "tenant_alert_settings" : "TenantId"
  "tenants" ||--o{ "tenant_audit_config" : "TenantId"
  "tenants" ||--o{ "tenant_data_retention_config" : "TenantId"
  "tenants" ||--o| "tenant_demo_config" : "Nocturne.Infrastructure.Data.Entities.TenantDemoConfigEntity,TenantId"
  "member_invites" ||--o{ "tenant_members" : "CreatedFromInviteId"
  "tenants" ||--o{ "tenant_members" : "TenantId"
  "tenant_members" ||--o{ "tenant_member_roles" : "TenantMemberId"
  "tenant_roles" ||--o{ "tenant_member_roles" : "TenantRoleId"
  "tenants" ||--o{ "tenant_roles" : "TenantId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `member_invites` | `CreatedBySubjectId` | `subjects` | Identity & Authentication |
| `tenant_members` | `SubjectId` | `subjects` | Identity & Authentication |

## Identity & Authentication

Tables: `subjects`, `subject_roles`, `subject_oidc_identities`, `subject_avatars`, `roles`, `refresh_tokens`, `recovery_codes`, `totp_credentials`, `passkey_credentials`, `oidc_providers`, `auth_audit_log`

```mermaid
erDiagram
  "subjects" {
    uuid id PK
    varchar500 access_request_message
    varchar64 access_token_hash
    varchar50 access_token_prefix
    varchar20 approval_status
    varchar2048 avatar_url
    timestamptz created_at
    varchar255 email
    bool is_active
    bool is_platform_admin
    bool is_system_subject
    timestamptz last_login_at
    varchar255 name
    text notes
    varchar24 original_id
    varchar10 preferred_language
    timestamptz updated_at
    varchar50 username
  }
  "subject_roles" {
    uuid subject_id PK,FK
    uuid role_id PK,FK
    timestamptz assigned_at
    uuid assigned_by_id FK
  }
  "subject_oidc_identities" {
    uuid id PK
    varchar255 email
    varchar500 issuer
    timestamptz last_used_at
    timestamptz linked_at
    varchar255 oidc_subject_id
    uuid provider_id FK
    uuid subject_id FK
  }
  "subject_avatars" {
    uuid id PK
    varchar64 content_type
    timestamptz created_at
    bytea data
    int file_size
    uuid subject_id FK
  }
  "roles" {
    uuid id PK
    timestamptz created_at
    varchar500 description
    bool is_system_role
    varchar100 name
    text notes
    varchar24 original_id
    timestamptz updated_at
  }
  "refresh_tokens" {
    uuid id PK
    timestamptz created_at
    varchar500 device_description
    timestamptz expires_at
    varchar45 ip_address
    timestamptz issued_at
    timestamptz last_used_at
    varchar255 oidc_session_id
    uuid replaced_by_token_id
    timestamptz revoked_at
    varchar255 revoked_reason
    uuid subject_id FK
    varchar64 token_hash
    timestamptz updated_at
    text user_agent
  }
  "recovery_codes" {
    uuid id PK
    varchar128 code_hash
    timestamptz created_at
    uuid subject_id FK
    timestamptz used_at
  }
  "totp_credentials" {
    uuid id PK
    timestamptz created_at
    varchar255 label
    timestamptz last_used_at
    bytea secret_key
    uuid subject_id FK
  }
  "passkey_credentials" {
    uuid id PK
    uuid aa_guid
    timestamptz created_at
    bytea credential_id
    varchar255 label
    timestamptz last_used_at
    bytea public_key
    bigint sign_count
    uuid subject_id FK
  }
  "oidc_providers" {
    uuid id PK
    varchar50 button_color
    jsonb claim_mappings
    varchar255 client_id
    bytea client_secret_encrypted
    timestamptz created_at
    timestamptz discovery_cached_at
    jsonb discovery_document
    int display_order
    varchar500 icon
    bool is_enabled
    varchar500 issuer_url
    varchar100 name
    jsonb oauth2_settings
    varchar32 provider_type
    timestamptz updated_at
  }
  "auth_audit_log" {
    uuid id PK
    varchar50 correlation_id
    timestamptz created_at
    jsonb details
    varchar500 error_message
    varchar50 event_type
    varchar45 ip_address
    uuid refresh_token_id FK
    uuid subject_id FK
    bool success
    text user_agent
  }
  "refresh_tokens" ||--o{ "auth_audit_log" : "RefreshTokenId"
  "subjects" ||--o{ "auth_audit_log" : "SubjectId"
  "subjects" ||--o{ "passkey_credentials" : "SubjectId"
  "subjects" ||--o{ "recovery_codes" : "SubjectId"
  "subjects" ||--o{ "refresh_tokens" : "SubjectId"
  "subjects" ||--o{ "subject_avatars" : "SubjectId"
  "oidc_providers" ||--o{ "subject_oidc_identities" : "ProviderId"
  "subjects" ||--o{ "subject_oidc_identities" : "SubjectId"
  "subjects" ||--o{ "subject_roles" : "AssignedById"
  "roles" ||--o{ "subject_roles" : "RoleId"
  "subjects" ||--o{ "subject_roles" : "SubjectId"
  "subjects" ||--o{ "totp_credentials" : "SubjectId"
```

## OAuth Server

Tables: `oauth_clients`, `oauth_authorization_codes`, `oauth_device_codes`, `oauth_grants`, `oauth_refresh_tokens`

```mermaid
erDiagram
  "oauth_clients" {
    uuid id PK
    varchar500 client_id
    varchar255 client_name
    varchar2048 client_uri
    timestamptz created_at
    varchar45 created_from_ip
    varchar255 display_name
    bool is_known
    varchar2048 logo_uri
    text redirect_uris
    varchar255 software_id
    uuid tenant_id FK
    timestamptz updated_at
  }
  "oauth_authorization_codes" {
    uuid id PK
    uuid client_entity_id FK
    varchar128 code_challenge
    varchar64 code_hash
    timestamptz created_at
    timestamptz expires_at
    bool limit_to_24_hours
    timestamptz redeemed_at
    varchar2000 redirect_uri
    uuid subject_id FK
    uuid tenant_id FK
  }
  "oauth_device_codes" {
    uuid id PK
    timestamptz approved_at
    varchar500 client_id
    timestamptz created_at
    timestamptz denied_at
    varchar64 device_code_hash
    timestamptz expires_at
    uuid grant_id FK
    int interval
    timestamptz last_polled_at
    uuid subject_id
    uuid tenant_id FK
    varchar20 user_code
  }
  "oauth_grants" {
    uuid id PK
    timestamptz activated_at
    varchar45 activated_ip
    text activated_user_agent
    uuid client_id FK
    timestamptz created_at
    uuid created_by_subject_id FK
    timestamptz dismissed_at
    timestamptz expires_at
    varchar50 grant_type
    bool is_migrated
    varchar255 label
    timestamptz last_used_at
    varchar45 last_used_ip
    text last_used_user_agent
    varchar128 legacy_secret_hash
    timestamptz revoked_at
    uuid subject_id FK
    uuid tenant_id FK
    varchar128 token_hash
  }
  "oauth_refresh_tokens" {
    uuid id PK
    timestamptz expires_at
    uuid grant_id FK
    timestamptz issued_at
    uuid replaced_by_id FK
    timestamptz revoked_at
    uuid tenant_id FK
    varchar64 token_hash
  }
  "oauth_clients" ||--o{ "oauth_authorization_codes" : "ClientEntityId"
  "oauth_grants" ||--o{ "oauth_device_codes" : "GrantId"
  "oauth_clients" ||--o{ "oauth_grants" : "ClientEntityId"
  "oauth_grants" ||--o{ "oauth_refresh_tokens" : "GrantId"
  "oauth_refresh_tokens" ||--o{ "oauth_refresh_tokens" : "ReplacedById"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `oauth_authorization_codes` | `SubjectId` | `subjects` | Identity & Authentication |
| `oauth_authorization_codes` | `TenantId` | `tenants` | Tenancy & Membership |
| `oauth_clients` | `TenantId` | `tenants` | Tenancy & Membership |
| `oauth_device_codes` | `TenantId` | `tenants` | Tenancy & Membership |
| `oauth_grants` | `CreatedBySubjectId` | `subjects` | Identity & Authentication |
| `oauth_grants` | `SubjectId` | `subjects` | Identity & Authentication |
| `oauth_grants` | `TenantId` | `tenants` | Tenancy & Membership |
| `oauth_refresh_tokens` | `TenantId` | `tenants` | Tenancy & Membership |

## Glucose & Vitals (v4)

Tables: `sensor_glucose`, `meter_glucose`, `calibrations`, `bg_checks`, `compression_low_suggestions`, `heart_rates`, `step_counts`, `body_weights`

```mermaid
erDiagram
  "sensor_glucose" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    double delta
    varchar256 device
    varchar32 direction
    double filtered
    varchar16 glucose_processing
    varchar64 legacy_id
    double mgdl
    int noise
    uuid patient_device_id FK
    double smoothed_mgdl
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    double trend_rate
    double unfiltered
    double unsmoothed_mgdl
    int utc_offset
  }
  "meter_glucose" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    varchar64 legacy_id
    double mgdl
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "calibrations" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    double intercept
    varchar64 legacy_id
    double scale
    double slope
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "bg_checks" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    double glucose
    varchar32 glucose_type
    varchar64 legacy_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    varchar32 units
    int utc_offset
  }
  "compression_low_suggestions" {
    uuid id PK
    double confidence
    bigint created_at
    double drop_rate
    bigint end_mills
    double lowest_glucose
    date night_of
    int recovery_minutes
    bigint reviewed_at
    bigint start_mills
    uuid state_span_id
    varchar20 status
    uuid tenant_id FK
  }
  "heart_rates" {
    uuid id PK
    int accuracy
    int bpm
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar255 device
    varchar255 entered_by
    varchar24 original_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "step_counts" {
    uuid id PK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar255 device
    varchar255 entered_by
    int metric
    varchar24 original_id
    int source
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "body_weights" {
    uuid id PK
    numeric body_fat_percent
    varchar50 created_at
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar255 device
    varchar255 entered_by
    numeric lean_mass_kg
    bigint mills
    varchar24 original_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    int utc_offset
    numeric weight_kg
  }
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `bg_checks` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `bg_checks` | `TenantId` | `tenants` | Tenancy & Membership |
| `body_weights` | `TenantId` | `tenants` | Tenancy & Membership |
| `calibrations` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `calibrations` | `TenantId` | `tenants` | Tenancy & Membership |
| `compression_low_suggestions` | `TenantId` | `tenants` | Tenancy & Membership |
| `heart_rates` | `TenantId` | `tenants` | Tenancy & Membership |
| `meter_glucose` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `meter_glucose` | `TenantId` | `tenants` | Tenancy & Membership |
| `sensor_glucose` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `sensor_glucose` | `PatientDeviceId` | `patient_devices` | Devices & Status Snapshots (v4) |
| `sensor_glucose` | `TenantId` | `tenants` | Tenancy & Membership |
| `step_counts` | `TenantId` | `tenants` | Tenancy & Membership |

## Insulin & Therapy (v4)

Tables: `boluses`, `bolus_calculations`, `basal_injections`, `temp_basals`, `carb_intakes`, `basal_schedules`, `carb_ratio_schedules`, `sensitivity_schedules`, `target_range_schedules`, `therapy_settings`, `patient_insulins`

```mermaid
erDiagram
  "boluses" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid aps_snapshot_id FK
    bool automatic
    uuid bolus_calculation_id FK
    varchar32 bolus_kind
    varchar32 bolus_type
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    double delivered
    varchar256 device
    uuid device_id FK
    double duration
    double insulin
    jsonb insulin_context
    varchar128 insulin_type
    varchar64 legacy_id
    uuid patient_device_id FK
    double programmed
    varchar256 pump_record_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    double unabsorbed
    int utc_offset
  }
  "bolus_calculations" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    double blood_glucose_input
    varchar256 blood_glucose_input_source
    varchar32 calculation_type
    double carb_input
    double carb_ratio
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    double entered_insulin
    double insulin_on_board
    double insulin_programmed
    double insulin_recommendation
    double insulin_recommendation_for_carbs
    varchar64 legacy_id
    double pre_bolus
    double split_ext
    double split_now
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "basal_injections" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    jsonb insulin_context
    varchar64 legacy_id
    varchar4096 notes
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    double units
    int utc_offset
  }
  "temp_basals" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid aps_snapshot_id FK
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    uuid device_id FK
    timestamptz end_timestamp
    jsonb insulin_context
    varchar64 legacy_id
    varchar32 origin
    uuid patient_device_id FK
    varchar256 pump_record_id
    double rate
    double scheduled_rate
    timestamptz start_timestamp
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    int utc_offset
  }
  "carb_intakes" {
    uuid id PK
    int absorption_time
    jsonb additional_properties
    varchar256 app
    double carb_time
    double carbs
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    varchar64 legacy_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "basal_schedules" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    jsonb entries_json
    varchar64 legacy_id
    varchar100 profile_name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "carb_ratio_schedules" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    jsonb entries_json
    varchar64 legacy_id
    varchar100 profile_name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "sensitivity_schedules" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    jsonb entries_json
    varchar64 legacy_id
    varchar100 profile_name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "target_range_schedules" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    jsonb entries_json
    varchar64 legacy_id
    varchar100 profile_name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "therapy_settings" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    int carbs_hr
    int carbs_hr_high
    int carbs_hr_low
    int carbs_hr_medium
    uuid correlation_id FK
    varchar256 data_source
    int delay
    int delay_high
    int delay_low
    int delay_medium
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    double dia
    varchar100 entered_by
    bool is_default
    bool is_externally_managed
    varchar64 legacy_id
    jsonb loop_settings_json
    bool per_gi_values
    varchar100 profile_name
    varchar50 start_date
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    varchar64 timezone
    varchar10 units
    int utc_offset
  }
  "patient_insulins" {
    uuid id PK
    int concentration
    varchar32 curve
    timestamptz deleted_at
    bool deleted_by_user
    double dia
    date end_date
    varchar64 formulation_id
    varchar32 insulin_category
    bool is_current
    bool is_primary
    varchar256 name
    varchar4096 notes
    int peak
    varchar16 role
    date start_date
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "bolus_calculations" ||--o{ "boluses" : "BolusCalculationId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `basal_injections` | `TenantId` | `tenants` | Tenancy & Membership |
| `basal_schedules` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `basal_schedules` | `TenantId` | `tenants` | Tenancy & Membership |
| `bolus_calculations` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `bolus_calculations` | `TenantId` | `tenants` | Tenancy & Membership |
| `boluses` | `ApsSnapshotId` | `aps_snapshots` | Devices & Status Snapshots (v4) |
| `boluses` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `boluses` | `DeviceId` | `devices` | Devices & Status Snapshots (v4) |
| `boluses` | `PatientDeviceId` | `patient_devices` | Devices & Status Snapshots (v4) |
| `boluses` | `TenantId` | `tenants` | Tenancy & Membership |
| `carb_intakes` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `carb_intakes` | `TenantId` | `tenants` | Tenancy & Membership |
| `carb_ratio_schedules` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `carb_ratio_schedules` | `TenantId` | `tenants` | Tenancy & Membership |
| `patient_insulins` | `TenantId` | `tenants` | Tenancy & Membership |
| `sensitivity_schedules` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `sensitivity_schedules` | `TenantId` | `tenants` | Tenancy & Membership |
| `target_range_schedules` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `target_range_schedules` | `TenantId` | `tenants` | Tenancy & Membership |
| `temp_basals` | `ApsSnapshotId` | `aps_snapshots` | Devices & Status Snapshots (v4) |
| `temp_basals` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `temp_basals` | `DeviceId` | `devices` | Devices & Status Snapshots (v4) |
| `temp_basals` | `PatientDeviceId` | `patient_devices` | Devices & Status Snapshots (v4) |
| `temp_basals` | `TenantId` | `tenants` | Tenancy & Membership |
| `therapy_settings` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `therapy_settings` | `TenantId` | `tenants` | Tenancy & Membership |

## Devices & Status Snapshots (v4)

Tables: `devices`, `device_events`, `device_status_extras`, `patient_devices`, `patient_records`, `aps_snapshots`, `pump_snapshots`, `uploader_snapshots`, `notes`

```mermaid
erDiagram
  "devices" {
    uuid id PK
    jsonb additional_properties
    varchar32 category
    timestamptz deleted_at
    bool deleted_by_user
    timestamptz first_seen_timestamp
    timestamptz last_seen_timestamp
    varchar128 serial
    uuid tenant_id FK
    varchar128 type
  }
  "device_events" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    uuid device_id FK
    varchar64 event_type
    varchar64 legacy_id
    varchar4096 notes
    uuid patient_device_id FK
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "device_status_extras" {
    uuid id PK
    uuid correlation_id
    timestamptz deleted_at
    bool deleted_by_user
    jsonb extras
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
  }
  "patient_devices" {
    uuid id PK
    varchar32 aid_algorithm
    varchar64 catalog_id
    timestamptz deleted_at
    bool deleted_by_user
    varchar32 device_category
    uuid device_id FK
    date end_date
    bool is_current
    varchar256 manufacturer
    varchar256 model
    varchar4096 notes
    varchar256 serial_number
    date start_date
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "patient_records" {
    uuid id PK
    varchar2048 avatar_url
    date date_of_birth
    timestamptz deleted_at
    bool deleted_by_user
    varchar32 diabetes_type
    varchar256 diabetes_type_other
    date diagnosis_date
    varchar256 preferred_name
    varchar64 pronouns
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    varchar64 timezone
  }
  "aps_snapshots" {
    uuid id PK
    jsonb additional_properties
    varchar32 aps_system
    varchar64 aid_version
    double basal_iob
    double bolus_iob
    double cob
    uuid correlation_id
    double current_bg
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    uuid device_id FK
    bool enacted
    double enacted_bolus_volume
    int enacted_duration
    jsonb enacted_json
    double enacted_rate
    double eventual_bg
    double iob
    varchar64 legacy_id
    jsonb loop_json
    uuid patient_device_id FK
    jsonb predicted_cob_json
    jsonb predicted_default_json
    jsonb predicted_iob_json
    timestamptz predicted_start_timestamp
    jsonb predicted_uam_json
    jsonb predicted_zt_json
    double recommended_bolus
    double sensitivity_ratio
    jsonb suggested_json
    timestamptz sys_created_at
    timestamptz sys_updated_at
    double target_bg
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "pump_snapshots" {
    uuid id PK
    jsonb additional_properties
    int battery_percent
    double battery_voltage
    double bolus_iob
    bool bolusing
    varchar64 clock
    uuid correlation_id
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    uuid device_id FK
    double iob
    varchar64 legacy_id
    varchar128 manufacturer
    varchar128 model
    uuid patient_device_id FK
    varchar64 pump_mode
    varchar64 pump_status
    double reservoir
    varchar64 reservoir_display
    bool suspended
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz timestamp
    int utc_offset
  }
  "uploader_snapshots" {
    uuid id PK
    jsonb additional_properties
    int battery
    double battery_voltage
    uuid correlation_id
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    uuid device_id FK
    bool is_charging
    varchar64 legacy_id
    varchar256 name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    double temperature
    uuid tenant_id FK
    timestamptz timestamp
    varchar128 type
    int utc_offset
  }
  "notes" {
    uuid id PK
    jsonb additional_properties
    varchar256 app
    uuid correlation_id FK
    varchar256 data_source
    timestamptz deleted_at
    bool deleted_by_user
    varchar256 device
    varchar256 event_type
    bool is_announcement
    varchar64 legacy_id
    varchar256 sync_identifier
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    varchar4096 text
    timestamptz timestamp
    int utc_offset
  }
  "devices" ||--o{ "aps_snapshots" : "DeviceId"
  "patient_devices" ||--o{ "aps_snapshots" : "PatientDeviceId"
  "devices" ||--o{ "device_events" : "DeviceId"
  "patient_devices" ||--o{ "device_events" : "PatientDeviceId"
  "devices" ||--o{ "patient_devices" : "DeviceId"
  "devices" ||--o{ "pump_snapshots" : "DeviceId"
  "patient_devices" ||--o{ "pump_snapshots" : "PatientDeviceId"
  "devices" ||--o{ "uploader_snapshots" : "DeviceId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `aps_snapshots` | `TenantId` | `tenants` | Tenancy & Membership |
| `device_events` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `device_events` | `TenantId` | `tenants` | Tenancy & Membership |
| `device_status_extras` | `TenantId` | `tenants` | Tenancy & Membership |
| `devices` | `TenantId` | `tenants` | Tenancy & Membership |
| `notes` | `CorrelationId` | `decomposition_batches` | Connectors & Migration |
| `notes` | `TenantId` | `tenants` | Tenancy & Membership |
| `patient_devices` | `TenantId` | `tenants` | Tenancy & Membership |
| `patient_records` | `TenantId` | `tenants` | Tenancy & Membership |
| `pump_snapshots` | `TenantId` | `tenants` | Tenancy & Membership |
| `uploader_snapshots` | `TenantId` | `tenants` | Tenancy & Membership |

## Food

Tables: `foods`, `treatment_foods`, `connector_food_entries`, `user_food_favorites`

```mermaid
erDiagram
  "foods" {
    uuid id PK
    jsonb additional_properties
    double carbs
    varchar200 category
    double energy
    varchar255 external_id
    varchar50 external_source
    double fat
    text foods
    int gi
    bool hidden
    bool hide_after_use
    varchar500 name
    varchar24 original_id
    double portion
    int position
    double protein
    varchar200 subcategory
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    varchar50 type
    varchar30 unit
  }
  "treatment_foods" {
    uuid id PK
    uuid carb_intake_id FK
    numeric carbs
    uuid food_id FK
    varchar1000 note
    numeric portions
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    int time_offset_minutes
  }
  "connector_food_entries" {
    uuid id PK
    numeric carbs
    varchar50 connector_source
    timestamptz consumed_at
    numeric energy
    varchar255 external_entry_id
    varchar255 external_food_id
    numeric fat
    uuid food_id FK
    timestamptz logged_at
    varchar50 meal_name
    numeric protein
    timestamptz resolved_at
    varchar100 serving_description
    numeric servings
    varchar20 status
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "user_food_favorites" {
    uuid id PK
    uuid food_id FK
    timestamptz sys_created_at
    uuid tenant_id FK
    varchar255 user_id
  }
  "foods" ||--o{ "connector_food_entries" : "FoodId"
  "foods" ||--o{ "treatment_foods" : "FoodId"
  "foods" ||--o{ "user_food_favorites" : "FoodId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `connector_food_entries` | `TenantId` | `tenants` | Tenancy & Membership |
| `foods` | `TenantId` | `tenants` | Tenancy & Membership |
| `treatment_foods` | `CarbIntakeId` | `carb_intakes` | Insulin & Therapy (v4) |
| `treatment_foods` | `TenantId` | `tenants` | Tenancy & Membership |
| `user_food_favorites` | `TenantId` | `tenants` | Tenancy & Membership |

## Alerts

Tables: `alert_rules`, `alert_rule_channels`, `alert_instances`, `alert_deliveries`, `alert_excursions`, `alert_invites`, `alert_condition_timers`, `alert_custom_sounds`, `alert_tracker_state`

```mermaid
erDiagram
  "alert_rules" {
    uuid id PK
    bool allow_through_dnd
    bool auto_resolve_enabled
    jsonb auto_resolve_params
    jsonb client_configuration
    jsonb condition_params
    varchar32 condition_type
    timestamptz created_at
    varchar512 description
    bool is_enabled
    varchar128 name
    varchar16 severity
    int sort_order
    uuid tenant_id FK
    timestamptz updated_at
  }
  "alert_rule_channels" {
    uuid id PK
    uuid alert_rule_id FK
    varchar32 channel_type
    timestamptz created_at
    varchar512 destination
    varchar128 destination_label
    jsonb metadata
    int sort_order
    uuid tenant_id FK
  }
  "alert_instances" {
    uuid id PK
    uuid alert_excursion_id FK
    bool is_test
    varchar32 resolution_reason
    timestamptz resolved_at
    int snooze_count
    timestamptz snoozed_until
    varchar16 status
    varchar32 suppression_reason
    uuid tenant_id FK
    timestamptz triggered_at
  }
  "alert_deliveries" {
    uuid id PK
    uuid alert_instance_id FK
    uuid alert_rule_channel_id FK
    varchar32 channel_type
    timestamptz created_at
    timestamptz delivered_at
    varchar512 destination
    bool is_test
    text last_error
    jsonb payload
    varchar256 platform_message_id
    varchar256 platform_thread_id
    int retry_count
    varchar16 status
    uuid tenant_id FK
  }
  "alert_excursions" {
    uuid id PK
    timestamptz acknowledged_at
    varchar256 acknowledged_by
    uuid alert_rule_id FK
    timestamptz ended_at
    timestamptz hysteresis_started_at
    timestamptz started_at
    uuid tenant_id FK
  }
  "alert_invites" {
    uuid id PK
    uuid alert_rule_channel_id FK
    timestamptz created_at
    uuid created_by
    timestamptz expires_at
    bool is_used
    varchar32 permission_scope
    uuid tenant_id FK
    varchar128 token
    uuid used_by
  }
  "alert_condition_timers" {
    uuid alert_rule_id PK,FK
    varchar512 condition_path PK
    timestamptz first_true_at
    uuid tenant_id FK
  }
  "alert_custom_sounds" {
    uuid id PK
    timestamptz created_at
    bytea data
    int file_size
    varchar64 mime_type
    varchar128 name
    uuid tenant_id FK
  }
  "alert_tracker_state" {
    uuid alert_rule_id PK,FK
    uuid active_excursion_id FK
    int confirmation_count
    varchar16 state
    uuid tenant_id FK
    timestamptz updated_at
  }
  "alert_rules" ||--o{ "alert_condition_timers" : "AlertRuleId"
  "alert_instances" ||--o{ "alert_deliveries" : "AlertInstanceId"
  "alert_rule_channels" ||--o{ "alert_deliveries" : "AlertRuleChannelId"
  "alert_rules" ||--o{ "alert_excursions" : "AlertRuleId"
  "alert_excursions" ||--o{ "alert_instances" : "AlertExcursionId"
  "alert_rule_channels" ||--o{ "alert_invites" : "AlertRuleChannelId"
  "alert_rules" ||--o{ "alert_rule_channels" : "AlertRuleId"
  "alert_excursions" ||--o{ "alert_tracker_state" : "ActiveExcursionId"
  "alert_rules" ||--o| "alert_tracker_state" : "Nocturne.Infrastructure.Data.Entities.AlertTrackerStateEntity,AlertRuleId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `alert_condition_timers` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_custom_sounds` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_deliveries` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_excursions` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_instances` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_invites` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_rule_channels` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_rules` | `TenantId` | `tenants` | Tenancy & Membership |
| `alert_tracker_state` | `TenantId` | `tenants` | Tenancy & Membership |

## Trackers

Tables: `tracker_definitions`, `tracker_instances`, `tracker_presets`, `tracker_notification_thresholds`, `state_spans`

```mermaid
erDiagram
  "tracker_definitions" {
    uuid id PK
    int category
    varchar100 completion_event_type
    timestamptz created_at
    int dashboard_visibility
    varchar1000 description
    varchar100 icon
    bool is_favorite
    int lifespan_hours
    int mode
    varchar255 name
    jsonb required_roles
    varchar100 start_event_type
    uuid tenant_id FK
    jsonb trigger_event_types
    varchar255 trigger_notes_contains
    timestamptz updated_at
    varchar255 user_id
    int visibility
  }
  "tracker_instances" {
    uuid id PK
    int ack_snooze_mins
    varchar255 complete_treatment_id
    timestamptz completed_at
    varchar1000 completion_notes
    int completion_reason
    uuid definition_id FK
    timestamptz last_acked_at
    timestamptz scheduled_at
    varchar1000 start_notes
    varchar255 start_treatment_id
    timestamptz started_at
    uuid tenant_id FK
    varchar255 user_id
  }
  "tracker_presets" {
    uuid id PK
    timestamptz created_at
    varchar1000 default_start_notes
    uuid definition_id FK
    varchar255 name
    uuid tenant_id FK
    varchar255 user_id
  }
  "tracker_notification_thresholds" {
    uuid id PK
    bool audio_enabled
    varchar100 audio_sound
    varchar500 description
    int display_order
    int hours
    int max_repeats
    bool push_enabled
    int repeat_interval_mins
    bool respect_quiet_hours
    uuid tenant_id FK
    uuid tracker_definition_id FK
    int urgency
    bool vibrate_enabled
  }
  "state_spans" {
    uuid id PK
    varchar50 category
    timestamptz created_at
    timestamptz end_timestamp
    jsonb metadata
    varchar255 original_id
    varchar50 source
    timestamptz start_timestamp
    varchar100 state
    uuid superseded_by_id FK
    uuid tenant_id FK
    timestamptz updated_at
  }
  "state_spans" ||--o{ "state_spans" : "SupersededById"
  "tracker_definitions" ||--o{ "tracker_instances" : "DefinitionId"
  "tracker_definitions" ||--o{ "tracker_notification_thresholds" : "TrackerDefinitionId"
  "tracker_definitions" ||--o{ "tracker_presets" : "DefinitionId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `state_spans` | `TenantId` | `tenants` | Tenancy & Membership |
| `tracker_definitions` | `TenantId` | `tenants` | Tenancy & Membership |
| `tracker_instances` | `TenantId` | `tenants` | Tenancy & Membership |
| `tracker_notification_thresholds` | `TenantId` | `tenants` | Tenancy & Membership |
| `tracker_presets` | `TenantId` | `tenants` | Tenancy & Membership |

## Connectors & Migration

Tables: `connector_configurations`, `data_source_metadata`, `migration_runs`, `migration_sources`, `linked_records`, `dedup_reconcile_state`, `decomposition_batches`, `discrepancy_analyses`, `discrepancy_details`

```mermaid
erDiagram
  "connector_configurations" {
    uuid id PK
    jsonb configuration
    varchar100 connector_name
    bool is_healthy
    timestamptz last_error_at
    varchar1000 last_error_message
    timestamptz last_modified
    timestamptz last_successful_sync
    timestamptz last_sync_attempt
    varchar200 modified_by
    int schema_version
    jsonb secrets
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
  }
  "data_source_metadata" {
    uuid id PK
    timestamptz archived_at
    timestamptz created_at
    varchar255 device_id
    bool is_archived
    varchar1000 notes
    uuid tenant_id FK
    timestamptz updated_at
  }
  "migration_runs" {
    uuid id PK
    timestamptz completed_at
    timestamptz date_range_end
    timestamptz date_range_start
    int entries_migrated
    text error_message
    uuid source_id FK
    timestamptz started_at
    varchar20 state
    int treatments_migrated
  }
  "migration_sources" {
    uuid id PK
    timestamptz created_at
    timestamptz last_migrated_data_timestamp
    timestamptz last_migration_at
    varchar20 mode
    text mongo_connection_string_encrypted
    varchar255 mongo_database_name
    varchar128 nightscout_api_secret_hash
    varchar512 nightscout_url
    varchar255 source_identifier
  }
  "linked_records" {
    uuid id PK
    uuid canonical_id
    varchar100 data_source
    bool is_primary
    uuid record_id
    varchar20 record_type
    bigint source_timestamp
    timestamptz sys_created_at
    uuid tenant_id FK
  }
  "dedup_reconcile_state" {
    uuid tenant_id PK,FK
    timestamptz last_reconciled_link_created_at
  }
  "decomposition_batches" {
    uuid id PK
    timestamptz created_at
    timestamptz deleted_at
    bool deleted_by_user
    varchar128 source
    varchar128 source_record_id
    uuid tenant_id FK
  }
  "discrepancy_analyses" {
    uuid id PK
    timestamptz analysis_timestamp
    bool body_match
    varchar128 correlation_id
    int critical_discrepancy_count
    varchar2000 error_message
    int major_discrepancy_count
    int minor_discrepancy_count
    bool nightscout_missing
    bigint nightscout_response_time_ms
    int nightscout_status_code
    bool nocturne_missing
    bigint nocturne_response_time_ms
    int nocturne_status_code
    int overall_match
    varchar10 request_method
    varchar2048 request_path
    varchar50 selected_response_target
    varchar500 selection_reason
    bool status_code_match
    varchar1000 summary
    uuid tenant_id FK
    bigint total_processing_time_ms
  }
  "discrepancy_details" {
    uuid id PK
    uuid analysis_id FK
    varchar1000 description
    int discrepancy_type
    varchar500 field
    varchar2000 nightscout_value
    varchar2000 nocturne_value
    timestamptz recorded_at
    int severity
    uuid tenant_id FK
  }
  "discrepancy_analyses" ||--o{ "discrepancy_details" : "AnalysisId"
  "migration_sources" ||--o{ "migration_runs" : "SourceId"
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `connector_configurations` | `TenantId` | `tenants` | Tenancy & Membership |
| `data_source_metadata` | `TenantId` | `tenants` | Tenancy & Membership |
| `decomposition_batches` | `TenantId` | `tenants` | Tenancy & Membership |
| `dedup_reconcile_state` | `TenantId` | `tenants` | Tenancy & Membership |
| `discrepancy_analyses` | `TenantId` | `tenants` | Tenancy & Membership |
| `discrepancy_details` | `TenantId` | `tenants` | Tenancy & Membership |
| `linked_records` | `TenantId` | `tenants` | Tenancy & Membership |

## Audit & Event Logs

Tables: `mutation_audit_log`, `read_access_log`, `system_events`

```mermaid
erDiagram
  "mutation_audit_log" {
    uuid id PK
    varchar10 action
    varchar50 auth_type
    jsonb changes
    varchar50 correlation_id
    timestamptz created_at
    varchar200 endpoint
    uuid entity_id
    varchar100 entity_type
    varchar45 ip_address
    uuid subject_id
    varchar128 subject_name
    uuid tenant_id FK
    uuid token_id
  }
  "read_access_log" {
    uuid id PK
    varchar8 api_secret_hash_prefix
    varchar50 auth_type
    varchar50 correlation_id
    timestamptz created_at
    varchar200 endpoint
    varchar100 entity_type
    varchar45 ip_address
    jsonb query_parameters
    int record_count
    int status_code
    uuid subject_id
    varchar128 subject_name
    uuid tenant_id FK
    uuid token_id
    text user_agent
  }
  "system_events" {
    uuid id PK
    varchar50 category
    varchar100 code
    timestamptz created_at
    varchar1000 description
    varchar50 event_type
    jsonb metadata
    bigint mills
    varchar255 original_id
    varchar50 source
    uuid tenant_id FK
  }
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `mutation_audit_log` | `TenantId` | `tenants` | Tenancy & Membership |
| `read_access_log` | `TenantId` | `tenants` | Tenancy & Membership |
| `system_events` | `TenantId` | `tenants` | Tenancy & Membership |

## Platform & Misc

Tables: `DataProtectionKeys`, `clock_faces`, `coach_mark_states`, `in_app_notifications`, `timezone_timeline`, `chat_identity_directory`, `chat_identity_pending_links`

```mermaid
erDiagram
  "DataProtectionKeys" {
    int id PK
    text FriendlyName
    text Xml
  }
  "clock_faces" {
    uuid id PK
    jsonb config
    timestamptz created_at
    varchar255 name
    timestamptz sys_created_at
    timestamptz sys_updated_at
    uuid tenant_id FK
    timestamptz updated_at
    varchar255 user_id
  }
  "coach_mark_states" {
    uuid id PK
    timestamptz completed_at
    varchar255 mark_key
    timestamptz seen_at
    varchar50 status
    uuid subject_id
    uuid tenant_id FK
  }
  "in_app_notifications" {
    uuid id PK
    jsonb actions_json
    varchar20 archive_reason
    timestamptz archived_at
    varchar20 category
    timestamptz created_at
    varchar50 icon
    bool is_archived
    jsonb metadata_json
    timestamptz read_at
    jsonb resolution_conditions_json
    varchar100 source
    varchar255 source_id
    varchar500 subtitle
    uuid tenant_id FK
    varchar255 title
    varchar100 type
    varchar20 urgency
    varchar255 user_id
  }
  "timezone_timeline" {
    uuid id PK
    timestamptz created_at
    timestamptz effective_from
    uuid tenant_id FK
    varchar64 timezone
    timestamptz updated_at
  }
  "chat_identity_directory" {
    uuid id PK
    timestamptz created_at
    varchar128 display_name
    varchar8 display_unit
    bool is_active
    bool is_default
    varchar64 label
    uuid nocturne_user_id
    varchar16 platform
    varchar256 platform_channel_id
    varchar256 platform_user_id
    timestamptz revoked_at
    uuid tenant_id
  }
  "chat_identity_pending_links" {
    varchar64 token PK
    timestamptz created_at
    timestamptz expires_at
    varchar16 platform
    varchar256 platform_user_id
    varchar32 source
    varchar64 tenant_slug
  }
```

**Cross-domain references**

| Table | Column(s) | References | Domain |
|---|---|---|---|
| `clock_faces` | `TenantId` | `tenants` | Tenancy & Membership |
| `coach_mark_states` | `TenantId` | `tenants` | Tenancy & Membership |
| `in_app_notifications` | `TenantId` | `tenants` | Tenancy & Membership |
| `timezone_timeline` | `TenantId` | `tenants` | Tenancy & Membership |


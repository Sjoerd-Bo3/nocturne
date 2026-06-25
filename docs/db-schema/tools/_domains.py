DOMAINS = [
 ("Tenancy & Membership", [
    "tenants","tenant_members","tenant_member_roles","tenant_roles","member_invites",
    "membership_requests","tenant_alert_settings","tenant_audit_config",
    "tenant_data_retention_config","tenant_demo_config","settings","platform_settings"]),
 ("Identity & Authentication", [
    "subjects","subject_roles","subject_oidc_identities","subject_avatars","roles",
    "refresh_tokens","recovery_codes","totp_credentials","passkey_credentials",
    "oidc_providers","auth_audit_log"]),
 ("OAuth Server", [
    "oauth_clients","oauth_authorization_codes","oauth_device_codes","oauth_grants",
    "oauth_refresh_tokens"]),
 ("Glucose & Vitals (v4)", [
    "sensor_glucose","meter_glucose","calibrations","bg_checks",
    "compression_low_suggestions","heart_rates","step_counts","body_weights"]),
 ("Insulin & Therapy (v4)", [
    "boluses","bolus_calculations","basal_injections","temp_basals","carb_intakes",
    "basal_schedules","carb_ratio_schedules","sensitivity_schedules",
    "target_range_schedules","therapy_settings","patient_insulins"]),
 ("Devices & Status Snapshots (v4)", [
    "devices","device_events","device_status_extras","patient_devices","patient_records",
    "aps_snapshots","pump_snapshots","uploader_snapshots","notes"]),
 ("Food", [
    "foods","treatment_foods","connector_food_entries","user_food_favorites"]),
 ("Alerts", [
    "alert_rules","alert_rule_channels","alert_instances","alert_deliveries",
    "alert_excursions","alert_invites","alert_condition_timers","alert_custom_sounds",
    "alert_tracker_state"]),
 ("Trackers", [
    "tracker_definitions","tracker_instances","tracker_presets",
    "tracker_notification_thresholds","state_spans"]),
 ("Connectors & Migration", [
    "connector_configurations","data_source_metadata","migration_runs","migration_sources",
    "linked_records","dedup_reconcile_state","decomposition_batches",
    "discrepancy_analyses","discrepancy_details"]),
 ("Audit & Event Logs", [
    "mutation_audit_log","read_access_log","system_events"]),
 ("Platform & Misc", [
    "DataProtectionKeys","clock_faces","coach_mark_states","in_app_notifications",
    "timezone_timeline","chat_identity_directory","chat_identity_pending_links"]),
]

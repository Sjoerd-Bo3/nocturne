using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nocturne.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRlsWithCheckClauses : Migration
    {
        /// <summary>
        /// All tenant-scoped tables that have existing USING-only RLS policies
        /// from the EnforceMultitenancy migration.
        /// </summary>
        private static readonly string[] TenantScopedTables =
        [
            "entries", "treatments", "devicestatus", "foods",
            "connector_food_entries", "treatment_foods", "user_food_favorites",
            "settings", "profiles", "activities", "step_counts", "heart_rates",
            "discrepancy_analyses", "discrepancy_details",
            "alert_rules", "alert_history",
            "notification_preferences", "emergency_contacts", "device_health",
            "data_source_metadata",
            "tracker_definitions", "tracker_instances", "tracker_presets",
            "tracker_notification_thresholds",
            "state_spans", "linked_records", "connector_configurations",
            "in_app_notifications", "clock_faces", "compression_low_suggestions",
            // V4 tables
            "sensor_glucose", "meter_glucose", "calibrations",
            "boluses", "carb_intakes", "bg_checks", "notes", "device_events",
            "bolus_calculations", "aps_snapshots", "pump_snapshots",
            "uploader_snapshots", "pump_devices", "temp_basals",
            "therapy_settings", "basal_schedules", "carb_ratio_schedules",
            "sensitivity_schedules", "target_range_schedules",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the existing USING-only policies and recreate them with both
            // USING (read enforcement) and WITH CHECK (write enforcement) clauses.
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {table};");
                migrationBuilder.Sql(
                    $"""
                    CREATE POLICY tenant_isolation ON {table}
                        USING (tenant_id = current_setting('app.current_tenant_id')::uuid)
                        WITH CHECK (tenant_id = current_setting('app.current_tenant_id')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert to USING-only policies (read enforcement only).
            foreach (var table in TenantScopedTables)
            {
                migrationBuilder.Sql($"DROP POLICY IF EXISTS tenant_isolation ON {table};");
                migrationBuilder.Sql(
                    $"""
                    CREATE POLICY tenant_isolation ON {table}
                        USING (tenant_id = current_setting('app.current_tenant_id')::uuid);
                    """);
            }
        }
    }
}

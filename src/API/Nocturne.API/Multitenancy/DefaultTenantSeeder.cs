using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Multitenancy;

/// <summary>
/// Startup service that ensures a default tenant exists and backfills
/// tenant_id on any existing data rows that predate multitenancy.
/// </summary>
public static class DefaultTenantSeeder
{
    public static async Task SeedDefaultTenantAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NocturneDbContext>>();

        // Check if any tenants exist
        var tenantCount = await context.Tenants.CountAsync();
        if (tenantCount > 0)
        {
            logger.LogDebug("Tenants already exist ({Count}), skipping default tenant seeding", tenantCount);
            return;
        }

        logger.LogInformation("No tenants found. Creating default tenant and backfilling tenant_id...");

        // Create default tenant
        var defaultTenant = new TenantEntity
        {
            Slug = "default",
            DisplayName = "Default",
            IsActive = true,
            IsDefault = true,
        };
        context.Tenants.Add(defaultTenant);
        await context.SaveChangesAsync();

        var tenantId = defaultTenant.Id;
        logger.LogInformation("Created default tenant with ID {TenantId}", tenantId);

        // Seed default roles for the tenant
        var roleService = scope.ServiceProvider.GetRequiredService<ITenantRoleService>();
        await roleService.SeedRolesForTenantAsync(tenantId);

        var ownerRole = await context.TenantRoles
            .FirstAsync(r => r.TenantId == tenantId && r.Slug == TenantPermissions.SeedRoles.Owner);

        // Assign all existing subjects as owners of the default tenant
        var subjects = await context.Subjects.ToListAsync();
        foreach (var subject in subjects)
        {
            var member = new TenantMemberEntity
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenantId,
                SubjectId = subject.Id,
                SysCreatedAt = DateTime.UtcNow,
                SysUpdatedAt = DateTime.UtcNow,
            };
            context.TenantMembers.Add(member);
            context.TenantMemberRoles.Add(new TenantMemberRoleEntity
            {
                Id = Guid.CreateVersion7(),
                TenantMemberId = member.Id,
                TenantRoleId = ownerRole.Id,
                SysCreatedAt = DateTime.UtcNow,
            });
        }
        await context.SaveChangesAsync();
        logger.LogInformation("Assigned {Count} existing subjects to default tenant", subjects.Count);

        // Backfill tenant_id on all tenant-scoped tables using raw SQL
        var tenantScopedTables = new[]
        {
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
        };

        foreach (var table in tenantScopedTables)
        {
            // Table names are from a hardcoded list above, not user input
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection
            var updated = await context.Database.ExecuteSqlRawAsync(
                $"UPDATE {table} SET tenant_id = {{0}} WHERE tenant_id IS NULL",
                tenantId);
#pragma warning restore EF1002

            if (updated > 0)
            {
                logger.LogInformation("Backfilled tenant_id on {Count} rows in {Table}", updated, table);
            }
        }

        logger.LogInformation("Default tenant seeding complete");
    }
}

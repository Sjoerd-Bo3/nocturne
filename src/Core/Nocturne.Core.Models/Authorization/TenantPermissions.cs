namespace Nocturne.Core.Models.Authorization;

/// <summary>
/// Permission atoms for the tenant RBAC system.
/// Uses the resource.action format compatible with OAuth scopes.
/// </summary>
public static class TenantPermissions
{
    // Data permissions (existing OAuth scopes)
    public const string EntriesRead = "entries.read";
    public const string EntriesReadWrite = "entries.readwrite";
    public const string TreatmentsRead = "treatments.read";
    public const string TreatmentsReadWrite = "treatments.readwrite";
    public const string DeviceStatusRead = "devicestatus.read";
    public const string DeviceStatusReadWrite = "devicestatus.readwrite";
    public const string ProfileRead = "profile.read";
    public const string ProfileReadWrite = "profile.readwrite";
    public const string NotificationsRead = "notifications.read";
    public const string NotificationsReadWrite = "notifications.readwrite";
    public const string ReportsRead = "reports.read";
    public const string HealthRead = "health.read";
    public const string IdentityRead = "identity.read";

    // Feature/admin permissions (new)
    public const string RolesManage = "roles.manage";
    public const string MembersInvite = "members.invite";
    public const string MembersManage = "members.manage";
    public const string TenantSettings = "tenant.settings";
    public const string SharingManage = "sharing.manage";
    public const string Superuser = "*";

    /// <summary>
    /// All valid permission atoms (excluding superuser).
    /// </summary>
    public static readonly HashSet<string> All =
    [
        EntriesRead, EntriesReadWrite,
        TreatmentsRead, TreatmentsReadWrite,
        DeviceStatusRead, DeviceStatusReadWrite,
        ProfileRead, ProfileReadWrite,
        NotificationsRead, NotificationsReadWrite,
        ReportsRead,
        HealthRead,
        IdentityRead,
        RolesManage,
        MembersInvite,
        MembersManage,
        TenantSettings,
        SharingManage,
    ];

    /// <summary>
    /// Seed role slugs.
    /// </summary>
    public static class SeedRoles
    {
        public const string Owner = "owner";
        public const string Admin = "admin";
        public const string Caretaker = "caretaker";
        public const string Follower = "follower";
    }

    /// <summary>
    /// Default permissions for each seed role.
    /// </summary>
    public static readonly Dictionary<string, List<string>> SeedRolePermissions = new()
    {
        [SeedRoles.Owner] = [Superuser],
        [SeedRoles.Admin] =
        [
            EntriesReadWrite, TreatmentsReadWrite, DeviceStatusReadWrite,
            ProfileReadWrite, NotificationsReadWrite, ReportsRead,
            HealthRead, IdentityRead,
            MembersInvite, MembersManage, TenantSettings, RolesManage, SharingManage,
        ],
        [SeedRoles.Caretaker] =
        [
            EntriesRead, TreatmentsReadWrite, DeviceStatusRead,
            ProfileRead, NotificationsRead, ReportsRead, HealthRead,
        ],
        [SeedRoles.Follower] = [EntriesRead, HealthRead],
    };

    /// <summary>
    /// Display names for seed roles.
    /// </summary>
    public static readonly Dictionary<string, string> SeedRoleNames = new()
    {
        [SeedRoles.Owner] = "Owner",
        [SeedRoles.Admin] = "Administrator",
        [SeedRoles.Caretaker] = "Caretaker",
        [SeedRoles.Follower] = "Follower",
    };

    /// <summary>
    /// Checks if a permission satisfies a required permission.
    /// Handles readwrite implying read, and superuser satisfying everything.
    /// </summary>
    public static bool Satisfies(string granted, string required)
    {
        if (granted == Superuser) return true;
        if (granted == required) return true;
        // readwrite implies read
        if (required.EndsWith(".read") && granted == required.Replace(".read", ".readwrite"))
            return true;
        return false;
    }

    /// <summary>
    /// Checks if a set of permissions satisfies a required permission.
    /// </summary>
    public static bool HasPermission(IEnumerable<string> permissions, string required)
    {
        return permissions.Any(p => Satisfies(p, required));
    }
}

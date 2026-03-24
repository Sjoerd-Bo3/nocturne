namespace Nocturne.Core.Contracts.Multitenancy;

public static class MembershipScopes
{
    public const string RoleOwner = "owner";
    public const string RoleAdmin = "admin";
    public const string RoleCaretaker = "caretaker";
    public const string RoleFollower = "follower";

    private static readonly List<string> AllScopes = ["*"];

    private static readonly List<string> CaretakerScopes =
    [
        "entries.readwrite", "treatments.readwrite", "devicestatus.readwrite",
        "profile.readwrite", "notifications.readwrite", "reports.read",
        "identity.read", "health.read"
    ];

    public static List<string> Resolve(string role, List<string>? memberScopes)
    {
        return role switch
        {
            RoleOwner or RoleAdmin => AllScopes,
            RoleCaretaker => CaretakerScopes,
            RoleFollower => memberScopes ?? [],
            _ => [],
        };
    }
}

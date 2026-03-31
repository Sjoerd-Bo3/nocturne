namespace Nocturne.Core.Models.V4;

public class NightscoutTransitionStatus
{
    public MigrationStatusInfo Migration { get; set; } = new();
    public WriteBackHealthInfo WriteBack { get; set; } = new();
    public DisconnectRecommendation Recommendation { get; set; } = new();
}

public class MigrationStatusInfo
{
    public Dictionary<string, int> RecordCounts { get; set; } = new();
    public DateTimeOffset? LastSyncTime { get; set; }
    public bool IsComplete { get; set; }
}

public class WriteBackHealthInfo
{
    public int RequestsLast24h { get; set; }
    public int SuccessesLast24h { get; set; }
    public int FailuresLast24h { get; set; }
    public bool CircuitBreakerOpen { get; set; }
    public DateTimeOffset? LastSuccessTime { get; set; }
}

public class DisconnectRecommendation
{
    /// <summary>not-ready, almost-ready, or safe</summary>
    public string Status { get; set; } = "not-ready";
    public List<string> Blockers { get; set; } = [];
    public int? StabilityDaysRemaining { get; set; }
}

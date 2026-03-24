namespace Nocturne.Core.Models;

/// <summary>
/// Snapshot of current sensor state provided to condition evaluators.
/// All glucose values are in mg/dL; rate is mg/dL per minute.
/// </summary>
public record SensorContext
{
    public required decimal? LatestValue { get; init; }
    public required DateTime? LatestTimestamp { get; init; }
    public required decimal? TrendRate { get; init; }
    public required DateTime? LastReadingAt { get; init; }
}

// ----- Condition parameter records (deserialized from JSONB) -----

public record ThresholdCondition(string Direction, decimal Value);
public record RateOfChangeCondition(string Direction, decimal Rate);
public record SignalLossCondition(int TimeoutMinutes);
public record CompositeCondition(string Operator, List<ConditionNode> Conditions);

public record ConditionNode(
    string Type,
    ThresholdCondition? Threshold = null,
    RateOfChangeCondition? RateOfChange = null,
    SignalLossCondition? SignalLoss = null,
    CompositeCondition? Composite = null
);

// Excursion tracker states
public enum TrackerState { Idle, Confirming, Active, Hysteresis }

// ----- Domain models for alert tracker persistence -----

/// <summary>
/// Per-rule state machine tracker. Maps 1:1 with an alert rule.
/// States: idle -> confirming -> active -> hysteresis -> idle.
/// </summary>
public class AlertTrackerState
{
    public Guid AlertRuleId { get; set; }
    public string State { get; set; } = "idle";
    public int ConfirmationCount { get; set; }
    public Guid? ActiveExcursionId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A composable alert rule with condition tree, hysteresis, and confirmation settings.
/// </summary>
public class AlertRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ConditionType { get; set; } = string.Empty;
    public string ConditionParams { get; set; } = "{}";
    public int HysteresisMinutes { get; set; }
    public int ConfirmationReadings { get; set; } = 1;
    public string Severity { get; set; } = "normal";
    public string ClientConfiguration { get; set; } = "{}";
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// A single continuous excursion (out-of-range episode) for a rule.
/// </summary>
public class AlertExcursion
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? HysteresisStartedAt { get; set; }
}

// Alert payload — what delivery providers receive (structured data, not pre-rendered text)
public record AlertPayload
{
    public required string AlertType { get; init; }
    public required string RuleName { get; init; }
    public required decimal? GlucoseValue { get; init; }
    public required string? Trend { get; init; }
    public required decimal? TrendRate { get; init; }
    public required DateTime ReadingTimestamp { get; init; }
    public required Guid ExcursionId { get; init; }
    public required Guid InstanceId { get; init; }
    public required Guid TenantId { get; init; }
    public required string SubjectName { get; init; }
    public required int ActiveExcursionCount { get; init; }
}

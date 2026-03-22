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

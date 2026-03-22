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

namespace Nocturne.Connectors.Nightscout.Services.WriteBack;

/// <summary>
/// Shared circuit breaker for all Nightscout write-back sinks.
/// Opens after consecutive failures and stays open for a recovery period
/// to avoid hammering an unavailable Nightscout instance.
/// </summary>
public class NightscoutCircuitBreaker
{
    private int _consecutiveFailures;
    private DateTimeOffset _openedAt;
    private const int FailureThreshold = 5;
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(60);

    public bool IsOpen =>
        _consecutiveFailures >= FailureThreshold
        && DateTimeOffset.UtcNow - _openedAt < RecoveryTimeout;

    public void RecordSuccess() => Interlocked.Exchange(ref _consecutiveFailures, 0);

    public void RecordFailure()
    {
        Interlocked.Increment(ref _consecutiveFailures);
        _openedAt = DateTimeOffset.UtcNow;
    }
}

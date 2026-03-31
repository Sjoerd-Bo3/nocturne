namespace Nocturne.Connectors.Nightscout.Services.WriteBack;

/// <summary>
/// Shared circuit breaker for all Nightscout write-back sinks.
/// Opens after consecutive failures and stays open for a recovery period
/// to avoid hammering an unavailable Nightscout instance.
/// Registered as singleton — all fields must be thread-safe.
/// </summary>
public class NightscoutCircuitBreaker
{
    private long _consecutiveFailures;
    private long _openedAtTicks;
    private const int FailureThreshold = 5;
    private static readonly TimeSpan RecoveryTimeout = TimeSpan.FromSeconds(60);

    public bool IsOpen =>
        Interlocked.Read(ref _consecutiveFailures) >= FailureThreshold
        && DateTimeOffset.UtcNow.Ticks - Interlocked.Read(ref _openedAtTicks) < RecoveryTimeout.Ticks;

    public void RecordSuccess()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
        Interlocked.Exchange(ref _openedAtTicks, 0);
    }

    public void RecordFailure()
    {
        Interlocked.Exchange(ref _openedAtTicks, DateTimeOffset.UtcNow.Ticks);
        Interlocked.Increment(ref _consecutiveFailures);
    }
}

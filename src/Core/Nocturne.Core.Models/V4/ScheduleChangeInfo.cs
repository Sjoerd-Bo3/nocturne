namespace Nocturne.Core.Models.V4;

/// <summary>
/// Tracks whether a schedule type changed during a given reporting period
/// </summary>
public class ScheduleChangeInfo
{
    /// <summary>Whether any change occurred during the period</summary>
    public bool ChangedDuringPeriod { get; set; }

    /// <summary>When the most recent change occurred (null if no changes)</summary>
    public DateTime? LastChangedAt { get; set; }

    /// <summary>How many distinct schedule versions exist within the period</summary>
    public int ChangeCount { get; set; }
}

namespace Nocturne.Core.Models.V4;

/// <summary>
/// A single time-of-day value in a therapy schedule (basal rate, carb ratio, ISF)
/// </summary>
public class ScheduleEntry
{
    /// <summary>
    /// Time in HH:mm format (e.g., "06:00")
    /// </summary>
    public string Time { get; set; } = "00:00";

    /// <summary>
    /// The value at this time (U/hr for basal, g/U for carb ratio, mg/dL per U for ISF)
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Time converted to seconds since midnight for faster lookups
    /// </summary>
    public int? TimeAsSeconds { get; set; }
}

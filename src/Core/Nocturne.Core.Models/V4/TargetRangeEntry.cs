namespace Nocturne.Core.Models.V4;

/// <summary>
/// A single time-of-day glucose target range entry with low and high bounds
/// </summary>
public class TargetRangeEntry
{
    /// <summary>
    /// Time in HH:mm format (e.g., "06:00")
    /// </summary>
    public string Time { get; set; } = "00:00";

    /// <summary>
    /// Low glucose target in mg/dL
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// High glucose target in mg/dL
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Time converted to seconds since midnight for faster lookups
    /// </summary>
    public int? TimeAsSeconds { get; set; }
}

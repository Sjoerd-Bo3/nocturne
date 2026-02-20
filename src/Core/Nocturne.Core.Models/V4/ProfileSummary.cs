namespace Nocturne.Core.Models.V4;

/// <summary>
/// Consolidated view of all V4 profile data: therapy settings and all schedule types
/// </summary>
public class ProfileSummary
{
    /// <summary>
    /// All therapy settings across all profile names
    /// </summary>
    public IEnumerable<TherapySettings> TherapySettings { get; set; } = [];

    /// <summary>
    /// All basal rate schedules across all profile names
    /// </summary>
    public IEnumerable<BasalSchedule> BasalSchedules { get; set; } = [];

    /// <summary>
    /// All carb ratio schedules across all profile names
    /// </summary>
    public IEnumerable<CarbRatioSchedule> CarbRatioSchedules { get; set; } = [];

    /// <summary>
    /// All insulin sensitivity schedules across all profile names
    /// </summary>
    public IEnumerable<SensitivitySchedule> SensitivitySchedules { get; set; } = [];

    /// <summary>
    /// All target range schedules across all profile names
    /// </summary>
    public IEnumerable<TargetRangeSchedule> TargetRangeSchedules { get; set; } = [];
}

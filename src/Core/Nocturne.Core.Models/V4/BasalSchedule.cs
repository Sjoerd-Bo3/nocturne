namespace Nocturne.Core.Models.V4;

/// <summary>
/// Time-of-day basal insulin rate schedule (U/hr), decomposed from a legacy <see cref="Profile"/> record.
/// </summary>
/// <remarks>
/// Each entry in <see cref="Entries"/> defines the basal rate in units per hour from a given time until the
/// next entry. Together with <see cref="CarbRatioSchedule"/>, <see cref="SensitivitySchedule"/>,
/// <see cref="TargetRangeSchedule"/>, and <see cref="TherapySettings"/>, this forms the complete V4
/// profile for a named profile store. All schedules decomposed from the same legacy <see cref="Profile"/>
/// share the same <see cref="IV4Record.CorrelationId"/>.
/// </remarks>
/// <seealso cref="Profile"/>
/// <seealso cref="IV4Record"/>
/// <seealso cref="ScheduleEntry"/>
/// <seealso cref="CarbRatioSchedule"/>
/// <seealso cref="SensitivitySchedule"/>
/// <seealso cref="TargetRangeSchedule"/>
/// <seealso cref="TherapySettings"/>
/// <seealso cref="ProfileSummary"/>
/// <seealso cref="TempBasal"/>
public class BasalSchedule : IV4Record
{
    /// <summary>
    /// UUID v7 primary key
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Canonical timestamp as UTC DateTime
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Unix milliseconds (computed from Timestamp for v1/v3 compatibility)
    /// </summary>
    public long Mills => new DateTimeOffset(Timestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// UTC offset in minutes
    /// </summary>
    public int? UtcOffset { get; set; }

    /// <summary>
    /// Device identifier that created this record
    /// </summary>
    public string? Device { get; set; }

    /// <summary>
    /// Application that uploaded this record
    /// </summary>
    public string? App { get; set; }

    /// <summary>
    /// Origin data source identifier
    /// </summary>
    public string? DataSource { get; set; }

    /// <summary>
    /// Links all V4 records decomposed from the same legacy Profile record
    /// </summary>
    public Guid? CorrelationId { get; set; }

    /// <summary>
    /// Composite legacy ID: "{profileId}:{storeName}" for migration traceability
    /// </summary>
    public string? LegacyId { get; set; }

    /// <summary>
    /// When this record was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When this record was last modified
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Named profile this schedule belongs to
    /// </summary>
    public string ProfileName { get; set; } = "Default";

    /// <summary>
    /// Basal rate entries throughout the day (time + U/hr value)
    /// </summary>
    public List<ScheduleEntry> Entries { get; set; } = [];

    /// <summary>
    /// Catch-all for fields not mapped to dedicated columns
    /// </summary>
    public Dictionary<string, object?>? AdditionalProperties { get; set; }
}

using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Glooko.Models;

/// <summary>
///     Response from /api/v3/devices_and_settings endpoint
/// </summary>
public class GlookoV3DeviceSettingsResponse
{
    [JsonPropertyName("deviceSettings")] public GlookoV3DeviceSettings? DeviceSettings { get; set; }
}

public class GlookoV3DeviceSettings
{
    /// <summary>
    ///     Dictionary keyed by device GUID, each containing timestamped settings snapshots
    /// </summary>
    [JsonPropertyName("pumps")]
    public Dictionary<string, Dictionary<string, GlookoV3PumpSettings>>? Pumps { get; set; }
}

public class GlookoV3PumpSettings
{
    [JsonPropertyName("syncTimestamp")] public string? SyncTimestamp { get; set; }

    [JsonPropertyName("generalSettings")] public GlookoV3GeneralSettings? GeneralSettings { get; set; }

    [JsonPropertyName("basalSettings")] public GlookoV3BasalSettings? BasalSettings { get; set; }

    [JsonPropertyName("profilesBolus")] public GlookoV3BolusProfile[]? ProfilesBolus { get; set; }

    [JsonPropertyName("pumpProfilesBasal")]
    public GlookoV3BasalProfile[]? PumpProfilesBasal { get; set; }
}

public class GlookoV3GeneralSettings
{
    [JsonPropertyName("activeInsulinTime")]
    public double ActiveInsulinTime { get; set; }

    [JsonPropertyName("bgGoalHigh")] public double BgGoalHigh { get; set; }

    [JsonPropertyName("bgGoalLow")] public double BgGoalLow { get; set; }
}

public class GlookoV3BasalSettings
{
    [JsonPropertyName("activeBasalProgram")]
    public string? ActiveBasalProgram { get; set; }
}

public class GlookoV3BolusProfile
{
    [JsonPropertyName("isfSegments")] public GlookoV3ProfileSegmentSet? IsfSegments { get; set; }

    [JsonPropertyName("targetBgSegments")]
    public GlookoV3TargetBgSegmentSet? TargetBgSegments { get; set; }

    [JsonPropertyName("insulinToCarbRatioSegments")]
    public GlookoV3ProfileSegmentSet? InsulinToCarbRatioSegments { get; set; }
}

public class GlookoV3BasalProfile
{
    [JsonPropertyName("segments")] public GlookoV3ProfileSegmentSet? Segments { get; set; }
}

public class GlookoV3ProfileSegmentSet
{
    [JsonPropertyName("profileName")] public string? ProfileName { get; set; }

    [JsonPropertyName("current")] public bool Current { get; set; }

    [JsonPropertyName("data")] public GlookoV3SegmentData[]? Data { get; set; }

    [JsonPropertyName("dailyTotal")] public double? DailyTotal { get; set; }
}

public class GlookoV3TargetBgSegmentSet
{
    [JsonPropertyName("profileName")] public string? ProfileName { get; set; }

    [JsonPropertyName("current")] public bool Current { get; set; }

    [JsonPropertyName("data")] public GlookoV3TargetBgSegmentData[]? Data { get; set; }
}

public class GlookoV3SegmentData
{
    /// <summary>
    ///     Start time in fractional hours (0.0 = midnight, 6.5 = 6:30am)
    /// </summary>
    [JsonPropertyName("segmentStart")]
    public double SegmentStart { get; set; }

    /// <summary>
    ///     Duration in hours
    /// </summary>
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    /// <summary>
    ///     Value (U/hr for basal, mg/dL per U for ISF, g per U for ICR)
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }
}

public class GlookoV3TargetBgSegmentData
{
    [JsonPropertyName("segmentStart")] public double SegmentStart { get; set; }

    [JsonPropertyName("duration")] public double Duration { get; set; }

    /// <summary>
    ///     Target BG value in mg/dL (single target, used when valueLow/valueHigh are 0)
    /// </summary>
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("valueLow")] public double ValueLow { get; set; }

    [JsonPropertyName("valueHigh")] public double ValueHigh { get; set; }
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Global matching settings for MyFitnessPal connector imports.
/// Controls how imported food entries are matched against existing treatments.
/// </summary>
public class MyFitnessPalMatchingSettings
{
    /// <summary>Time window (in minutes) within which a MyFitnessPal entry can match an existing treatment.</summary>
    [JsonPropertyName("matchTimeWindowMinutes")]
    public int MatchTimeWindowMinutes { get; set; } = 30;

    /// <summary>Maximum percentage difference in carbs allowed for a match (relative tolerance).</summary>
    [JsonPropertyName("matchCarbTolerancePercent")]
    public int MatchCarbTolerancePercent { get; set; } = 20;

    /// <summary>Maximum absolute difference in carb grams allowed for a match.</summary>
    [JsonPropertyName("matchCarbToleranceGrams")]
    public int MatchCarbToleranceGrams { get; set; } = 10;

    /// <summary>Whether to send notifications when matches are found or conflicts detected.</summary>
    [JsonPropertyName("enableMatchNotifications")]
    public bool EnableMatchNotifications { get; set; } = true;
}

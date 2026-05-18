using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents the glucose trend direction indicators used by Nightscout.
/// These values indicate the rate and direction of glucose change.
/// 1:1 Legacy JavaScript compatibility with ClientApp/lib/plugins/direction.js
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Direction
{
    /// <summary>
    /// No direction information available
    /// </summary>
    NONE,

    /// <summary>
    /// Rising very rapidly (>3 mg/dL per minute)
    /// </summary>
    TripleUp,

    /// <summary>
    /// Rising rapidly (2-3 mg/dL per minute)
    /// </summary>
    DoubleUp,

    /// <summary>
    /// Rising (1-2 mg/dL per minute)
    /// </summary>
    SingleUp,

    /// <summary>
    /// Rising slowly (0.5-1 mg/dL per minute)
    /// </summary>
    FortyFiveUp,

    /// <summary>
    /// Stable (change less than 0.5 mg/dL per minute)
    /// </summary>
    Flat,

    /// <summary>
    /// Falling slowly (0.5-1 mg/dL per minute)
    /// </summary>
    FortyFiveDown,

    /// <summary>
    /// Falling (1-2 mg/dL per minute)
    /// </summary>
    SingleDown,

    /// <summary>
    /// Falling rapidly (2-3 mg/dL per minute)
    /// </summary>
    DoubleDown,

    /// <summary>
    /// Falling very rapidly (>3 mg/dL per minute)
    /// </summary>
    TripleDown,

    /// <summary>
    /// CGM cannot determine direction due to insufficient data
    /// </summary>
    [JsonPropertyName("NOT COMPUTABLE")]
    NotComputable,

    /// <summary>
    /// Rate of change is outside measurable range
    /// </summary>
    [JsonPropertyName("RATE OUT OF RANGE")]
    RateOutOfRange,

    /// <summary>
    /// CGM sensor error or malfunction
    /// </summary>
    [JsonPropertyName("CGM ERROR")]
    CgmError,
}

/// <summary>
/// Extension methods for Direction enum
/// </summary>
public static class DirectionExtensions
{
    /// <summary>
    /// Converts a <see cref="Direction"/> enum value to the Nightscout/Dexcom trend number (0-9).
    /// Used by the pebble endpoint and other legacy integrations.
    /// </summary>
    /// <param name="direction">The glucose trend direction to convert</param>
    /// <returns>
    /// An integer 0-9 matching the Dexcom trend number convention:
    /// 0=None, 1=DoubleUp, 2=SingleUp, 3=FortyFiveUp, 4=Flat,
    /// 5=FortyFiveDown, 6=SingleDown, 7=DoubleDown, 8=NotComputable, 9=RateOutOfRange
    /// </returns>
    public static int ToTrendNumber(this Direction direction)
    {
        return direction switch
        {
            Direction.NONE => 0,
            Direction.DoubleUp => 1,
            Direction.SingleUp => 2,
            Direction.FortyFiveUp => 3,
            Direction.Flat => 4,
            Direction.FortyFiveDown => 5,
            Direction.SingleDown => 6,
            Direction.DoubleDown => 7,
            Direction.TripleUp => 1,      // Map to DoubleUp (closest)
            Direction.TripleDown => 7,    // Map to DoubleDown (closest)
            Direction.NotComputable => 8,
            Direction.RateOutOfRange => 9,
            Direction.CgmError => 8,      // Map to NotComputable
            _ => 8
        };
    }

    /// <summary>
    /// Parses a direction string to the corresponding Dexcom trend number (0-9).
    /// Handles both <see cref="Direction"/> enum names and legacy space-separated string formats
    /// (e.g., "NOT COMPUTABLE", "RATE OUT OF RANGE").
    /// </summary>
    /// <param name="direction">Direction string to parse; null or empty returns 8 (NotComputable)</param>
    /// <returns>Dexcom trend number (0-9); returns 8 (NotComputable) for any unrecognized input</returns>
    public static int ParseToTrendNumber(string? direction)
    {
        if (string.IsNullOrEmpty(direction))
            return 8; // NOT COMPUTABLE

        // Try to parse as enum first
        if (Enum.TryParse<Direction>(direction, ignoreCase: true, out var parsed))
        {
            return parsed.ToTrendNumber();
        }

        // Handle legacy string formats
        return direction.ToUpperInvariant() switch
        {
            "NOT COMPUTABLE" => 8,
            "RATE OUT OF RANGE" => 9,
            "CGM ERROR" => 8,
            _ => 8
        };
    }
}

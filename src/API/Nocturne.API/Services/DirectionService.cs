using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Static direction helpers with 1:1 legacy JavaScript compatibility.
/// Implements the exact algorithms from ClientApp/lib/plugins/direction.js and ClientApp/lib/plugins/bgnow.js.
/// All methods are pure functions with no dependencies.
/// </summary>
public static class DirectionService
{
    /// <summary>
    /// Direction to character mapping - exact legacy mapping from direction.js
    /// </summary>
    private static readonly Dictionary<Direction, string> DirectionCharMap = new()
    {
        { Direction.NONE, "⇼" },
        { Direction.TripleUp, "⤊" },
        { Direction.DoubleUp, "⇈" },
        { Direction.SingleUp, "↑" },
        { Direction.FortyFiveUp, "↗" },
        { Direction.Flat, "→" },
        { Direction.FortyFiveDown, "↘" },
        { Direction.SingleDown, "↓" },
        { Direction.DoubleDown, "⇊" },
        { Direction.TripleDown, "⤋" },
        { Direction.NotComputable, "-" },
        { Direction.RateOutOfRange, "⇕" },
    };

    /// <summary>
    /// Get direction information for display - exact legacy algorithm
    /// </summary>
    public static Core.Models.DirectionInfo GetDirectionInfo(Entry? entry)
    {
        var result = new Core.Models.DirectionInfo { Display = null };

        if (entry == null)
            return result;

        // Parse direction from string to enum
        var direction = ParseDirection(entry.Direction);
        result.Value = direction;
        result.Label = DirectionToChar(direction);
        result.Entity = CharToEntity(result.Label);

        return result;
    }

    /// <summary>
    /// Calculate glucose delta between current and previous readings - exact legacy algorithm
    /// </summary>
    public static Core.Models.DeltaInfo? CalculateDelta(IList<Entry> entries, string units)
    {
        if (entries.Count < 2)
            return null;

        var sortedEntries = entries.OrderByDescending(e => e.Mills).ToList();
        var recent = sortedEntries[0];
        var previous = sortedEntries[1];

        // Get mean values (use Mgdl if available, otherwise Sgv)
        var recentMean = recent.Mgdl != 0 ? recent.Mgdl : recent.Sgv ?? 0;
        var previousMean = previous.Mgdl != 0 ? previous.Mgdl : previous.Sgv ?? 0;

        if (recentMean == 0 || previousMean == 0)
            return null;

        // Calculate absolute difference and elapsed time
        var absolute = recentMean - previousMean;
        var elapsedMins = (recent.Mills - previous.Mills) / (60.0 * 1000); // Legacy interpolation logic - exactly matching bgnow.js
        var isInterpolated = elapsedMins > 9; // Legacy uses 9 minutes threshold        // Calculate mean5MinsAgo - exact legacy algorithm
        var mean5MinsAgo = isInterpolated
            ? recentMean - ((recentMean - previousMean) / elapsedMins) * 5
            : recentMean - (recentMean - previousMean);

        // Calculate mg/dL delta - exact legacy rounding
        var mgdl = (int)Math.Round(recentMean - mean5MinsAgo);

        // Scale for display - legacy scaling logic
        var scaled =
            units == "mmol"
                ? RoundBGToDisplayFormat(ScaleMgdl(recentMean) - ScaleMgdl(mean5MinsAgo))
                : mgdl; // Format display - exact legacy formatting
        var display =
            units == "mmol"
                ? (scaled >= 0 ? $"+{scaled:F1}" : $"{scaled:F1}")
                : (scaled >= 0 ? $"+{scaled}" : scaled.ToString());
        return new Core.Models.DeltaInfo
        {
            Absolute = absolute,
            ElapsedMins = elapsedMins,
            Interpolated = isInterpolated,
            Mean5MinsAgo = mean5MinsAgo,
            Mgdl = mgdl,
            Scaled = scaled,
            Display = display,
            Previous = previous,
            Current = recent,
            Times = new Dictionary<string, long>
            {
                ["recent"] = recent.Mills,
                ["previous"] = previous.Mills,
            },
        };
    }

    /// <summary>
    /// Calculate direction from slope and delta values
    /// </summary>
    public static Direction CalculateDirection(double current, double previous, double deltaMinutes)
    {
        if (deltaMinutes <= 0)
            return Direction.NONE;

        var mgdlDelta = current - previous;
        var slope = mgdlDelta / deltaMinutes; // mg/dL per minute        // Apply direction thresholds - based on legacy Nightscout thresholds
        return slope switch
        {
            > 3 => Direction.TripleUp,
            > 2 => Direction.DoubleUp,
            >= 1 => Direction.SingleUp,
            > 0 => Direction.FortyFiveUp,
            > -0.5 => Direction.Flat,
            > -1 => Direction.FortyFiveDown,
            > -2 => Direction.SingleDown,
            >= -3 => Direction.DoubleDown,
            _ => Direction.TripleDown,
        };
    }

    /// <summary>
    /// Get direction character mapping - exact legacy mapping
    /// </summary>
    public static string DirectionToChar(Direction direction)
    {
        return DirectionCharMap.TryGetValue(direction, out var character) ? character : "-";
    }

    /// <summary>
    /// Convert character to HTML entity - exact legacy method
    /// </summary>
    public static string CharToEntity(string character)
    {
        if (string.IsNullOrEmpty(character) || character.Length == 0)
            return string.Empty;

        return $"&#{(int)character[0]};";
    }

    /// <summary>
    /// Parse direction string to enum - handles legacy string values
    /// </summary>
    private static Direction ParseDirection(string? directionString)
    {
        if (string.IsNullOrEmpty(directionString))
            return Direction.NONE;

        return directionString switch
        {
            "NONE" => Direction.NONE,
            "TripleUp" => Direction.TripleUp,
            "DoubleUp" => Direction.DoubleUp,
            "SingleUp" => Direction.SingleUp,
            "FortyFiveUp" => Direction.FortyFiveUp,
            "Flat" => Direction.Flat,
            "FortyFiveDown" => Direction.FortyFiveDown,
            "SingleDown" => Direction.SingleDown,
            "DoubleDown" => Direction.DoubleDown,
            "TripleDown" => Direction.TripleDown,
            "NOT COMPUTABLE" => Direction.NotComputable,
            "RATE OUT OF RANGE" => Direction.RateOutOfRange,
            "CGM ERROR" => Direction.CgmError,
            _ => Direction.NONE,
        };
    }

    /// <summary>
    /// Scale mg/dL to mmol/L - exact legacy conversion
    /// </summary>
    private static double ScaleMgdl(double mgdl)
    {
        return mgdl / 18.0;
    }

    /// <summary>
    /// Round BG to display format - exact legacy rounding
    /// </summary>
    private static double RoundBGToDisplayFormat(double value)
    {
        return Math.Round(value, 1);
    }
}

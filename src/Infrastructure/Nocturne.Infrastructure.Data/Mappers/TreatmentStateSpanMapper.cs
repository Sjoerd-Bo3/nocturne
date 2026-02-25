using Nocturne.Core.Models;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Utility for identifying temp basal treatments by event type.
/// Used by the treatment routing logic to direct temp basals to the V4 TempBasal table.
/// </summary>
public static class TreatmentStateSpanMapper
{
    /// <summary>
    /// Event types that indicate a temp basal treatment (case-insensitive)
    /// </summary>
    private static readonly string[] TempBasalEventTypes =
    [
        "Temp Basal",
        "Temp Basal Start",
        "TempBasal"
    ];

    /// <summary>
    /// Determines if a treatment is a temp basal treatment
    /// </summary>
    /// <param name="treatment">The treatment to check</param>
    /// <returns>True if the treatment is a temp basal, false otherwise</returns>
    public static bool IsTempBasalTreatment(Treatment treatment)
    {
        if (treatment == null || string.IsNullOrEmpty(treatment.EventType))
            return false;

        return TempBasalEventTypes.Any(
            eventType => string.Equals(treatment.EventType, eventType, StringComparison.OrdinalIgnoreCase)
        );
    }
}

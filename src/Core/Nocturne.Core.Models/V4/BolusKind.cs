using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Distinguishes how a bolus was initiated
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BolusKind>))]
public enum BolusKind
{
    /// <summary>
    /// User-initiated bolus (correction, meal, etc.)
    /// </summary>
    Manual,

    /// <summary>
    /// Algorithm-delivered micro-dose (SMB) from an APS system
    /// </summary>
    Algorithm,
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Distinguishes how a <see cref="Bolus"/> was initiated.
/// </summary>
/// <remarks>
/// <see cref="Manual"/> covers all user-directed doses: meal boluses, correction boluses, and
/// boluses produced by a bolus calculator (<see cref="BolusCalculation"/>).
/// <see cref="Algorithm"/> covers micro-doses automatically delivered by APS systems
/// (Super Micro Boluses in OpenAPS/AAPS/Trio, automated boluses in Loop).
/// </remarks>
/// <seealso cref="Bolus"/>
/// <seealso cref="ApsSnapshot"/>
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

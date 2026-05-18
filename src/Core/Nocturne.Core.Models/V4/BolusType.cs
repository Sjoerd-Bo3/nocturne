using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Type of insulin bolus delivery shape.
/// </summary>
/// <seealso cref="Bolus"/>
[JsonConverter(typeof(JsonStringEnumConverter<BolusType>))]
public enum BolusType
{
    /// <summary>Standard immediate bolus delivery.</summary>
    Normal,

    /// <summary>Extended/square-wave bolus delivered over a <see cref="Bolus.Duration"/>.</summary>
    Square,

    /// <summary>Dual-wave bolus: part immediate, part extended.</summary>
    Dual
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// How the bolus calculation was determined.
/// </summary>
/// <seealso cref="BolusCalculation"/>
[JsonConverter(typeof(JsonStringEnumConverter<CalculationType>))]
public enum CalculationType
{
    /// <summary>Calculator suggested a dose; user may have adjusted.</summary>
    Suggested,

    /// <summary>User manually entered the dose without calculator assistance.</summary>
    Manual,

    /// <summary>Dose was automatically determined by an APS algorithm.</summary>
    Automatic
}

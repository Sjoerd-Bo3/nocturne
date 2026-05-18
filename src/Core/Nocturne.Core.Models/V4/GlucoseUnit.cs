using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Unit of measurement for glucose values.
/// </summary>
/// <seealso cref="BGCheck"/>
[JsonConverter(typeof(JsonStringEnumConverter<GlucoseUnit>))]
public enum GlucoseUnit
{
    /// <summary>Milligrams per deciliter (mg/dL).</summary>
    MgDl,

    /// <summary>Millimoles per liter (mmol/L).</summary>
    Mmol
}

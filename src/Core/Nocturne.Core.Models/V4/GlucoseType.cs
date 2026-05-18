using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Source type of a glucose reading.
/// </summary>
/// <seealso cref="BGCheck"/>
[JsonConverter(typeof(JsonStringEnumConverter<GlucoseType>))]
public enum GlucoseType
{
    /// <summary>Finger-stick blood glucose test.</summary>
    Finger,

    /// <summary>CGM or flash glucose sensor reading.</summary>
    Sensor
}

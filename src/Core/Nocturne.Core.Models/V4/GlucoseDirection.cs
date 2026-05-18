using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Direction of glucose change based on CGM arrow display.
/// </summary>
/// <remarks>
/// Ordinal values map 1:1 to <see cref="GlucoseTrend"/> for numeric trend conversion.
/// Used by <see cref="SensorGlucose.Direction"/>; the computed <see cref="SensorGlucose.Trend"/>
/// property casts this enum to <see cref="GlucoseTrend"/>.
/// </remarks>
/// <seealso cref="GlucoseTrend"/>
/// <seealso cref="SensorGlucose"/>
[JsonConverter(typeof(JsonStringEnumConverter<GlucoseDirection>))]
public enum GlucoseDirection
{
    /// <summary>No direction available.</summary>
    None,

    /// <summary>Glucose rising rapidly (more than +3 mg/dL/min).</summary>
    DoubleUp,

    /// <summary>Glucose rising (+2 to +3 mg/dL/min).</summary>
    SingleUp,

    /// <summary>Glucose rising slightly (+1 to +2 mg/dL/min).</summary>
    FortyFiveUp,

    /// <summary>Glucose stable (-1 to +1 mg/dL/min).</summary>
    Flat,

    /// <summary>Glucose falling slightly (-1 to -2 mg/dL/min).</summary>
    FortyFiveDown,

    /// <summary>Glucose falling (-2 to -3 mg/dL/min).</summary>
    SingleDown,

    /// <summary>Glucose falling rapidly (more than -3 mg/dL/min).</summary>
    DoubleDown,

    /// <summary>Direction cannot be computed from available data.</summary>
    NotComputable,

    /// <summary>Rate of change is outside the computable range.</summary>
    RateOutOfRange
}

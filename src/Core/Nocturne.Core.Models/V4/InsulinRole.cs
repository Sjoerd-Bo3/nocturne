using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Describes the delivery role of an insulin formulation for a patient.
/// </summary>
/// <seealso cref="PatientInsulin"/>
/// <seealso cref="InsulinFormulation"/>
[JsonConverter(typeof(JsonStringEnumConverter<InsulinRole>))]
public enum InsulinRole
{
    /// <summary>Used only for bolus (mealtime/correction) doses.</summary>
    Bolus,

    /// <summary>Used only for basal (background) doses.</summary>
    Basal,

    /// <summary>Used for both bolus and basal doses (typical for rapid-acting analogues in pumps).</summary>
    Both
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Pharmacokinetic category of an insulin formulation.
/// </summary>
/// <seealso cref="InsulinFormulation"/>
/// <seealso cref="PatientInsulin"/>
/// <seealso cref="InsulinCatalog"/>
[JsonConverter(typeof(JsonStringEnumConverter<InsulinCategory>))]
public enum InsulinCategory
{
    /// <summary>Rapid-acting analogue (e.g., Humalog, NovoRapid, Fiasp). Onset ~15 min, peak 1-2 h.</summary>
    RapidActing,

    /// <summary>Short-acting (regular) insulin (e.g., Humulin R). Onset ~30 min, peak 2-4 h.</summary>
    ShortActing,

    /// <summary>Intermediate-acting insulin (e.g., NPH). Onset 1-2 h, peak 4-8 h.</summary>
    IntermediateActing,

    /// <summary>Long-acting basal analogue (e.g., Lantus, Levemir). Onset 1-2 h, no pronounced peak.</summary>
    LongActing,

    /// <summary>Ultra-long-acting basal analogue (e.g., Tresiba, Toujeo). Duration up to 42 h.</summary>
    UltraLongActing,

    /// <summary>Pre-mixed insulin combining rapid/short and intermediate/long components.</summary>
    Premixed
}

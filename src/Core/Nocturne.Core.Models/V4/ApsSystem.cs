using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Identifies which APS system produced a device status snapshot
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ApsSystem>))]
public enum ApsSystem
{
    /// <summary>
    /// Vanilla OpenAPS (oref0/oref1 running on a dedicated rig)
    /// </summary>
    OpenAps,

    /// <summary>
    /// AndroidAPS (AAPS) — Android-based closed-loop system using oref algorithm
    /// </summary>
    AndroidAps,

    /// <summary>
    /// Loop for iOS
    /// </summary>
    Loop,

    /// <summary>
    /// Trio (formerly FreeAPS X) — iOS-based closed-loop system using oref algorithm
    /// </summary>
    Trio
}

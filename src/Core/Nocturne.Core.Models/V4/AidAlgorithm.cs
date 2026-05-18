using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Identifies the AID (Automated Insulin Delivery) algorithm running on a pump device.
/// Separate from hardware -- the same pump can run different algorithms.
/// </summary>
/// <remarks>
/// Used by <see cref="ApsSnapshot.AidAlgorithm"/> and <see cref="PatientDevice.AidAlgorithm"/>
/// to classify which control algorithm produced a given snapshot or is associated with a device.
/// Open-source AIDs are detected from <see cref="ApsSnapshot"/> data; commercial AIDs are
/// inferred from <see cref="TempBasal"/> patterns.
/// </remarks>
/// <seealso cref="ApsSnapshot"/>
/// <seealso cref="PatientDevice"/>
[JsonConverter(typeof(JsonStringEnumConverter<AidAlgorithm>))]
public enum AidAlgorithm
{
    /// <summary>Open-source OpenAPS algorithm.</summary>
    OpenAps,

    /// <summary>AndroidAPS (AAPS) algorithm.</summary>
    AndroidAps,

    /// <summary>Loop algorithm (iOS).</summary>
    Loop,

    /// <summary>Trio (formerly FreeAPS X) algorithm.</summary>
    Trio,

    /// <summary>iAPS algorithm.</summary>
    IAPS,

    /// <summary>Tandem Control-IQ commercial algorithm.</summary>
    ControlIQ,

    /// <summary>CamAPS FX commercial algorithm.</summary>
    CamAPSFX,

    /// <summary>Insulet Omnipod 5 commercial algorithm.</summary>
    Omnipod5Algorithm,

    /// <summary>Medtronic SmartGuard commercial algorithm.</summary>
    MedtronicSmartGuard,

    /// <summary>No AID algorithm (manual mode).</summary>
    None,

    /// <summary>Algorithm could not be determined.</summary>
    Unknown
}

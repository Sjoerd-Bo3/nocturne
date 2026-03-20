using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Identifies the AID (Automated Insulin Delivery) algorithm running on a pump device.
/// Separate from hardware — the same pump can run different algorithms.
/// Replaces the narrower ApsSystem enum.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AidAlgorithm>))]
public enum AidAlgorithm
{
    // Open-source AIDs (ApsSnapshot-based detection)
    OpenAps,
    AndroidAps,
    Loop,
    Trio,
    IAPS,

    // Commercial AIDs (TBR-based detection)
    ControlIQ,
    CamAPSFX,
    Omnipod5Algorithm,
    MedtronicSmartGuard,

    // Non-AID
    None,
    Unknown
}

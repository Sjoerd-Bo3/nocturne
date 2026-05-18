using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Category of a diabetes management device.
/// </summary>
/// <seealso cref="Device"/>
/// <seealso cref="PatientDevice"/>
/// <seealso cref="DeviceCatalogEntry"/>
[JsonConverter(typeof(JsonStringEnumConverter<DeviceCategory>))]
public enum DeviceCategory
{
    /// <summary>Insulin pump (e.g., Omnipod, t:slim, MiniMed).</summary>
    InsulinPump,

    /// <summary>Continuous glucose monitor (e.g., Dexcom G7, Libre 3).</summary>
    CGM,

    /// <summary>Blood glucose meter (finger-stick device).</summary>
    GlucoseMeter,

    /// <summary>Traditional insulin pen.</summary>
    InsulinPen,

    /// <summary>Smart insulin pen with dose tracking (e.g., NovoPen 6, InPen).</summary>
    SmartPen,

    /// <summary>Uploader device (phone, bridge, or relay that uploads data to the server).</summary>
    Uploader
}

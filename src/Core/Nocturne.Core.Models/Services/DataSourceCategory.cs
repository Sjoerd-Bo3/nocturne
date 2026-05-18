using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Services;

/// <summary>
/// Category of a data source or uploader app.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataSourceCategory>))]
public enum DataSourceCategory
{
    /// <summary>Continuous glucose monitor app or connector.</summary>
    [EnumMember(Value = "cgm"), JsonStringEnumMemberName("cgm")]
    Cgm,

    /// <summary>Automated insulin delivery system.</summary>
    [EnumMember(Value = "aid-system"), JsonStringEnumMemberName("aid-system")]
    AidSystem,

    /// <summary>General uploader app (e.g. Nightscout Uploader).</summary>
    [EnumMember(Value = "uploader"), JsonStringEnumMemberName("uploader")]
    Uploader,

    /// <summary>Insulin pump.</summary>
    [EnumMember(Value = "pump"), JsonStringEnumMemberName("pump")]
    Pump,

    /// <summary>Server-side connector pulling data from a cloud service.</summary>
    [EnumMember(Value = "connector"), JsonStringEnumMemberName("connector")]
    Connector,

    /// <summary>Manually entered data.</summary>
    [EnumMember(Value = "manual"), JsonStringEnumMemberName("manual")]
    Manual,

    /// <summary>Category could not be determined.</summary>
    [EnumMember(Value = "unknown"), JsonStringEnumMemberName("unknown")]
    Unknown,
}

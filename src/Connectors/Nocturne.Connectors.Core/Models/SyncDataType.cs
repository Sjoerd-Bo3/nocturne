using System.Text.Json.Serialization;

namespace Nocturne.Connectors.Core.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SyncDataType
{
    Glucose,
    ManualBG,
    Boluses,
    CarbIntake,
    BolusCalculations,
    Notes,
    DeviceEvents,
    StateSpans,
    Profiles,
    DeviceStatus,
    Activity,
    Food
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

[JsonConverter(typeof(JsonStringEnumConverter<InsulinRole>))]
public enum InsulinRole
{
    Bolus,
    Basal,
    Both
}

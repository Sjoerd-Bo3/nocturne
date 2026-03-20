using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

[JsonConverter(typeof(JsonStringEnumConverter<DeviceCategory>))]
public enum DeviceCategory { InsulinPump, CGM, GlucoseMeter, InsulinPen, SmartPen, Uploader }

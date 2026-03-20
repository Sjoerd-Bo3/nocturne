using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

[JsonConverter(typeof(JsonStringEnumConverter<DiabetesType>))]
public enum DiabetesType { Type1, Type2, LADA, MODY, Gestational, Other }

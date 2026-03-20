using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.V4;

[JsonConverter(typeof(JsonStringEnumConverter<InsulinCategory>))]
public enum InsulinCategory { RapidActing, ShortActing, IntermediateActing, LongActing, UltraLongActing, Premixed }

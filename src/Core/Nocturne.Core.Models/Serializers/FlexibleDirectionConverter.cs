using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Serializers;

/// <summary>
/// JSON converter for glucose trend direction that handles both string values
/// ("Flat", "SingleUp") and numeric values (1-9) from older Nightscout records.
/// </summary>
/// <remarks>
/// Numeric direction mapping follows the Dexcom/Nightscout convention:
/// 1=DoubleUp, 2=SingleUp, 3=FortyFiveUp, 4=Flat, 5=FortyFiveDown,
/// 6=SingleDown, 7=DoubleDown, 8=NOT COMPUTABLE, 9=RATE OUT OF RANGE.
/// </remarks>
/// <seealso cref="Entry"/>
public class FlexibleDirectionConverter : JsonConverter<string?>
{
    private static readonly Dictionary<int, string> NumericDirectionMap = new()
    {
        [1] = "DoubleUp",
        [2] = "SingleUp",
        [3] = "FortyFiveUp",
        [4] = "Flat",
        [5] = "FortyFiveDown",
        [6] = "SingleDown",
        [7] = "DoubleDown",
        [8] = "NOT COMPUTABLE",
        [9] = "RATE OUT OF RANGE",
    };

    public override string? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var intValue))
                {
                    return NumericDirectionMap.TryGetValue(intValue, out var direction)
                        ? direction
                        : intValue.ToString();
                }
                return null;

            case JsonTokenType.Null:
                return null;

            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

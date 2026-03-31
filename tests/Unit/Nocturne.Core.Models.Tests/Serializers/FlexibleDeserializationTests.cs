using System.Text.Json;
using FluentAssertions;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.Core.Models.Tests.Serializers;

/// <summary>
/// Tests that Entry and Treatment models correctly deserialize non-numeric string values
/// found in real-world OpenAPS Data Commons data.
/// </summary>
public class FlexibleDeserializationTests
{
    // ========================================================================
    // Entry.Noise — real data contains "Clean" instead of a number
    // ========================================================================

    [Fact]
    public void Entry_Noise_DeserializesNumericValue()
    {
        var json = """{"noise": 1}""";
        var entry = JsonSerializer.Deserialize<Entry>(json);

        entry!.Noise.Should().Be(1);
    }

    [Fact]
    public void Entry_Noise_DeserializesCleanStringAsNull()
    {
        var json = """{"noise": "Clean"}""";
        var entry = JsonSerializer.Deserialize<Entry>(json);

        entry!.Noise.Should().BeNull();
    }

    [Fact]
    public void Entry_Noise_DeserializesNumericStringAsInt()
    {
        var json = """{"noise": "3"}""";
        var entry = JsonSerializer.Deserialize<Entry>(json);

        entry!.Noise.Should().Be(3);
    }

    [Fact]
    public void Entry_Noise_DeserializesNullAsNull()
    {
        var json = """{"noise": null}""";
        var entry = JsonSerializer.Deserialize<Entry>(json);

        entry!.Noise.Should().BeNull();
    }

    [Fact]
    public void Entry_Noise_SerializesAsNumber()
    {
        var entry = new Entry { Noise = 2 };
        var json = JsonSerializer.Serialize(entry);

        json.Should().Contain("\"noise\":2");
    }

    // ========================================================================
    // Treatment.Rate — real data contains "offset" instead of a number
    // ========================================================================

    [Fact]
    public void Treatment_Rate_DeserializesNumericValue()
    {
        var json = """{"rate": 1.5}""";
        var treatment = JsonSerializer.Deserialize<Treatment>(json);

        treatment!.Rate.Should().Be(1.5);
    }

    [Fact]
    public void Treatment_Rate_DeserializesOffsetStringAsNull()
    {
        var json = """{"rate": "offset"}""";
        var treatment = JsonSerializer.Deserialize<Treatment>(json);

        treatment!.Rate.Should().BeNull();
    }

    [Fact]
    public void Treatment_Rate_DeserializesNumericStringAsDouble()
    {
        var json = """{"rate": "0.75"}""";
        var treatment = JsonSerializer.Deserialize<Treatment>(json);

        treatment!.Rate.Should().Be(0.75);
    }

    [Fact]
    public void Treatment_Rate_DeserializesNullAsNull()
    {
        var json = """{"rate": null}""";
        var treatment = JsonSerializer.Deserialize<Treatment>(json);

        treatment!.Rate.Should().BeNull();
    }
}

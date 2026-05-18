using System.Globalization;
using Nocturne.Core.Oref;
using Nocturne.Core.Oref.Models;

namespace Nocturne.API.Tests.Services.Devices;

[Parity("iob.test.js")]
public class OrefIobParityTests
{
    [Fact]
    public void CalculateIob_WithTreatments_ShouldMatchLegacyCurve()
    {
        if (!OrefService.IsAvailable())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var profile = CreateProfile(dia: 3.0);
        var treatments = new List<OrefTreatment>
        {
            new()
            {
                Insulin = 1.0,
                Mills = now.ToUnixTimeMilliseconds() - 1,
                EventType = "Bolus",
            },
        };

        var rightAfter = OrefService.CalculateIob(profile, treatments, now);
        Assert.NotNull(rightAfter);
        Assert.Equal(1.0, rightAfter!.Iob, 2);

        var afterHour = OrefService.CalculateIob(profile, treatments, now.AddHours(1));
        Assert.NotNull(afterHour);
        Assert.True(afterHour!.Iob < 1.0);
        Assert.True(afterHour.Iob > 0.0);

        var afterDia = OrefService.CalculateIob(profile, treatments, now.AddHours(3));
        Assert.NotNull(afterDia);
        Assert.Equal(0.0, afterDia!.Iob, 3);
    }

    [Fact]
    public void CalculateIob_WhenApproachingZero_ShouldNotGoNegative()
    {
        if (!OrefService.IsAvailable())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var profile = CreateProfile(dia: 3.0);
        var treatments = new List<OrefTreatment>
        {
            new()
            {
                Insulin = 5.0,
                Mills = now.ToUnixTimeMilliseconds(),
                EventType = "Bolus",
            },
        };

        var nearZero = OrefService.CalculateIob(
            profile,
            treatments,
            now.AddHours(3).AddSeconds(-90)
        );

        Assert.NotNull(nearZero);
        var display = NormalizeNegativeZero(nearZero!.Iob.ToString("F2", CultureInfo.InvariantCulture));
        Assert.Equal("0.00", display);
    }

    [Fact]
    public void CalculateIob_WithFourHourDia_ShouldLastLonger()
    {
        if (!OrefService.IsAvailable())
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var profile = CreateProfile(dia: 4.0);
        var treatments = new List<OrefTreatment>
        {
            new()
            {
                Insulin = 1.0,
                Mills = now.ToUnixTimeMilliseconds() - 1,
                EventType = "Bolus",
            },
        };

        var rightAfter = OrefService.CalculateIob(profile, treatments, now);
        var afterHour = OrefService.CalculateIob(profile, treatments, now.AddHours(1));
        var afterThreeHours = OrefService.CalculateIob(profile, treatments, now.AddHours(3));
        var afterFourHours = OrefService.CalculateIob(profile, treatments, now.AddHours(4));

        Assert.NotNull(rightAfter);
        Assert.Equal(1.0, rightAfter!.Iob, 2);
        Assert.True(afterHour!.Iob > 0.5);
        Assert.True(afterThreeHours!.Iob > 0.0);
        Assert.Equal(0.0, afterFourHours!.Iob, 3);
    }

    private static OrefProfile CreateProfile(double dia)
    {
        return new OrefProfile
        {
            Dia = dia,
            Sens = 0.0,
            CarbRatio = 10.0,
            Curve = "bilinear",
        };
    }

    private static string NormalizeNegativeZero(string value)
    {
        if (
            value.StartsWith("-", StringComparison.Ordinal)
            && double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var parsedValue
            )
            && parsedValue == 0
        )
        {
            return value[1..];
        }

        return value;
    }
}

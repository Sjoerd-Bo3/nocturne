using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

public class TbrBasedStrategy : IAidDetectionStrategy
{
    private const double DefaultDurationMinutes = 5.0;

    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.ControlIQ,
        AidAlgorithm.CamAPSFX,
        AidAlgorithm.Omnipod5Algorithm,
        AidAlgorithm.MedtronicSmartGuard
    };

    public AidSegmentMetrics CalculateMetrics(AidDetectionContext context)
    {
        var tempBasals = context.TempBasals;

        if (tempBasals.Count == 0)
        {
            return new AidSegmentMetrics();
        }

        var totalMinutes = (context.EndDate - context.StartDate).TotalMinutes;

        var algorithmMinutes = 0.0;
        var allMinutes = 0.0;

        foreach (var tbr in tempBasals)
        {
            var duration = GetClampedDuration(tbr, context.StartDate, context.EndDate);

            allMinutes += duration;

            if (tbr.Origin == TempBasalOrigin.Algorithm)
            {
                algorithmMinutes += duration;
            }
        }

        return new AidSegmentMetrics
        {
            AidActivePercent = Math.Min(algorithmMinutes / totalMinutes * 100.0, 100.0),
            PumpUsePercent = Math.Min(allMinutes / totalMinutes * 100.0, 100.0),
            LoopCycleCount = null,
            EnactedCount = null
        };
    }

    private static double GetClampedDuration(TempBasal tbr, DateTime windowStart, DateTime windowEnd)
    {
        var start = tbr.StartTimestamp < windowStart ? windowStart : tbr.StartTimestamp;
        var end = tbr.EndTimestamp.HasValue
            ? (tbr.EndTimestamp.Value > windowEnd ? windowEnd : tbr.EndTimestamp.Value)
            : tbr.StartTimestamp.AddMinutes(DefaultDurationMinutes);

        if (end > windowEnd) end = windowEnd;
        if (start >= end) return 0.0;

        return (end - start).TotalMinutes;
    }
}

using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

/// <summary>
/// Calculates <see cref="AidSegmentMetrics"/> for AID systems that express algorithm activity
/// through temporary basal rate (TBR) records rather than APS snapshots.
/// </summary>
/// <remarks>
/// Durations are clamped to the evaluation window defined by
/// <see cref="AidDetectionContext.StartDate"/> and <see cref="AidDetectionContext.EndDate"/>.
/// TBRs without an explicit end time are assumed to last <c>5</c> minutes.
/// <see cref="AidSegmentMetrics.LoopCycleCount"/> and <see cref="AidSegmentMetrics.EnactedCount"/>
/// are not applicable for this strategy and are returned as <see langword="null"/>.
/// </remarks>
/// <seealso cref="IAidDetectionStrategy"/>
public class TbrBasedStrategy : IAidDetectionStrategy
{
    private const double DefaultDurationMinutes = 5.0;

    /// <inheritdoc/>
    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.ControlIQ,
        AidAlgorithm.CamAPSFX,
        AidAlgorithm.Omnipod5Algorithm,
        AidAlgorithm.MedtronicSmartGuard
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Returns an empty <see cref="AidSegmentMetrics"/> when no TBR records are present
    /// in the supplied <paramref name="context"/>.
    /// </remarks>
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

    /// <summary>
    /// Returns the duration (in minutes) of <paramref name="tbr"/> clipped to the
    /// <paramref name="windowStart"/>–<paramref name="windowEnd"/> evaluation window.
    /// </summary>
    /// <param name="tbr">The temporary basal record to measure.</param>
    /// <param name="windowStart">Inclusive start of the evaluation window.</param>
    /// <param name="windowEnd">Inclusive end of the evaluation window.</param>
    /// <returns>
    /// Duration in minutes within the window, or <c>0.0</c> if the record falls entirely outside it.
    /// </returns>
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

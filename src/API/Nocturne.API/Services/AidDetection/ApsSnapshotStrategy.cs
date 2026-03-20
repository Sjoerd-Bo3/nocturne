using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

public class ApsSnapshotStrategy : IAidDetectionStrategy
{
    private const double LoopCycleIntervalMinutes = 5.0;

    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.OpenAps,
        AidAlgorithm.AndroidAps,
        AidAlgorithm.Loop,
        AidAlgorithm.Trio,
        AidAlgorithm.IAPS
    };

    public AidSegmentMetrics CalculateMetrics(AidDetectionContext context)
    {
        var snapshots = context.ApsSnapshots;

        if (snapshots.Count == 0)
        {
            return new AidSegmentMetrics();
        }

        var totalMinutes = (context.EndDate - context.StartDate).TotalMinutes;
        var totalCount = snapshots.Count;
        var enactedCount = snapshots.Count(s => s.Enacted);

        return new AidSegmentMetrics
        {
            LoopCycleCount = totalCount,
            EnactedCount = enactedCount,
            AidActivePercent = Math.Min(enactedCount * LoopCycleIntervalMinutes / totalMinutes * 100.0, 100.0),
            PumpUsePercent = Math.Min(totalCount * LoopCycleIntervalMinutes / totalMinutes * 100.0, 100.0)
        };
    }
}

using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.AidDetection;

/// <summary>
/// Strategy for detecting and calculating metrics for a specific automated insulin delivery (AID) algorithm.
/// Each implementation targets one or more <see cref="AidAlgorithm"/> variants and knows how to
/// extract algorithm-specific metrics from <see cref="AidDetectionContext"/> data.
/// </summary>
/// <seealso cref="IAidMetricsService"/>
/// <seealso cref="AidAlgorithm"/>
/// <seealso cref="AidSegmentMetrics"/>
public interface IAidDetectionStrategy
{
    /// <summary>
    /// The set of <see cref="AidAlgorithm"/> variants this strategy can handle.
    /// </summary>
    IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; }

    /// <summary>
    /// Calculate algorithm-specific metrics from the provided detection context.
    /// </summary>
    /// <param name="context">Context containing device segments, APS snapshots, and temp basals for analysis.</param>
    /// <returns>Computed <see cref="AidSegmentMetrics"/> for the segment.</returns>
    AidSegmentMetrics CalculateMetrics(AidDetectionContext context);
}

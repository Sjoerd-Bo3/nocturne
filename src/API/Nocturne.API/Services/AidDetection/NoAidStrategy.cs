using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

/// <summary>
/// Fallback <see cref="IAidDetectionStrategy"/> for tenants with no recognised AID algorithm.
/// Always returns an empty <see cref="AidSegmentMetrics"/>.
/// </summary>
/// <seealso cref="IAidDetectionStrategy"/>
public class NoAidStrategy : IAidDetectionStrategy
{
    /// <inheritdoc/>
    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.None,
        AidAlgorithm.Unknown
    };

    /// <inheritdoc/>
    /// <returns>A default (all-zero) <see cref="AidSegmentMetrics"/> instance.</returns>
    public AidSegmentMetrics CalculateMetrics(AidDetectionContext context)
    {
        return new AidSegmentMetrics();
    }
}

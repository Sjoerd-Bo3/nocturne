using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.AidDetection;

public class NoAidStrategy : IAidDetectionStrategy
{
    public IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; } = new HashSet<AidAlgorithm>
    {
        AidAlgorithm.None,
        AidAlgorithm.Unknown
    };

    public AidSegmentMetrics CalculateMetrics(AidDetectionContext context)
    {
        return new AidSegmentMetrics();
    }
}

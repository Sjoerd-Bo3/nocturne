using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts;

public interface IAidDetectionStrategy
{
    IReadOnlySet<AidAlgorithm> SupportedAlgorithms { get; }
    AidSegmentMetrics CalculateMetrics(AidDetectionContext context);
}

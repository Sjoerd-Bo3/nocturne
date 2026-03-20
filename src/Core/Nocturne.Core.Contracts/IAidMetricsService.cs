using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts;

public interface IAidMetricsService
{
    AidSystemMetrics Calculate(
        IReadOnlyList<DeviceSegmentInput> deviceSegments,
        IReadOnlyList<ApsSnapshot> apsSnapshots,
        IReadOnlyList<TempBasal> tempBasals,
        int siteChangeCount,
        double? cgmUsePercent,
        double? cgmActivePercent,
        double? targetLow,
        double? targetHigh,
        DateTime startDate,
        DateTime endDate);
}

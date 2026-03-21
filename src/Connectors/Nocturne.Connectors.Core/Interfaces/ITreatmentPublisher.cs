using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Interfaces;

public interface ITreatmentPublisher
{
    Task<bool> PublishTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        string source,
        CancellationToken cancellationToken = default);

    Task<DateTime?> GetLatestTreatmentTimestampAsync(
        string source,
        CancellationToken cancellationToken = default);
}

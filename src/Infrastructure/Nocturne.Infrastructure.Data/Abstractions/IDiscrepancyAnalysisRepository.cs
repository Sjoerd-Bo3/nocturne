using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.Infrastructure.Data.Abstractions;

/// <summary>
/// Repository port for discrepancy analysis operations
/// </summary>
public interface IDiscrepancyAnalysisRepository
{
    Task<Guid> StoreAnalysisAsync(
        string correlationId,
        DateTimeOffset analysisTimestamp,
        string requestMethod,
        string requestPath,
        int overallMatch,
        bool statusCodeMatch,
        bool bodyMatch,
        int? nightscoutStatusCode,
        int? nocturneStatusCode,
        long? nightscoutResponseTimeMs,
        long? nocturneResponseTimeMs,
        long totalProcessingTimeMs,
        string summary,
        string? selectedResponseTarget,
        string? selectionReason,
        List<DiscrepancyDetailData> discrepancies,
        bool nightscoutMissing = false,
        bool nocturneMissing = false,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<DiscrepancyAnalysisEntity>> GetAnalysesAsync(
        string? requestPath = null,
        int? overallMatch = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        int count = 100,
        int skip = 0,
        CancellationToken cancellationToken = default);

    Task<CompatibilityMetrics> GetCompatibilityMetricsAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<EndpointMetrics>> GetEndpointMetricsAsync(
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOldAnalysesAsync(
        DateTimeOffset cutoffDate,
        CancellationToken cancellationToken = default);
}

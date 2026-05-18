using Nocturne.Core.Models.Services;

namespace Nocturne.Core.Contracts.Analytics;

/// <summary>
/// Service for aggregating data overview statistics across all data types
/// </summary>
public interface IDataOverviewService
{
    /// <summary>
    /// Get the list of years that contain data and available data sources
    /// </summary>
    Task<DataOverviewYearsResponse> GetAvailableYearsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get day-level aggregated counts and average glucose for a given year
    /// </summary>
    /// <param name="year">The year to aggregate</param>
    /// <param name="dataSources">Optional data source filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<DailySummaryResponse> GetDailySummaryAsync(int year, string[]? dataSources = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get monthly GRI (Glycemic Risk Index) scores for a given year
    /// </summary>
    /// <param name="year">The year to compute GRI timeline for</param>
    /// <param name="dataSources">Optional data source filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<GriTimelineResponse> GetGriTimelineAsync(int year, string[]? dataSources = null, CancellationToken cancellationToken = default);
}

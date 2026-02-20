using Nocturne.Core.Models.Services;

namespace Nocturne.Core.Contracts;

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
    /// <param name="dataSource">Optional data source filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<DailySummaryResponse> GetDailySummaryAsync(int year, string? dataSource = null, CancellationToken cancellationToken = default);
}

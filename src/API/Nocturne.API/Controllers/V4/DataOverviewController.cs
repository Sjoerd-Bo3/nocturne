using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Services;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Aggregated data overview for heatmap visualization.
/// Provides year-level availability and day-level record counts.
/// </summary>
[ApiController]
[Route("api/v4/data-overview")]
[Produces("application/json")]
[Tags("V4 Data Overview")]
[ClientPropertyName("dataOverview")]
public class DataOverviewController : ControllerBase
{
    private readonly IDataOverviewService _dataOverviewService;
    private readonly ILogger<DataOverviewController> _logger;

    public DataOverviewController(
        IDataOverviewService dataOverviewService,
        ILogger<DataOverviewController> logger
    )
    {
        _dataOverviewService = dataOverviewService;
        _logger = logger;
    }

    /// <summary>
    /// Get the list of years that contain data and available data sources
    /// </summary>
    [HttpGet("years")]
    [RemoteQuery]
    [ProducesResponseType(typeof(DataOverviewYearsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DataOverviewYearsResponse>> GetAvailableYears(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var result = await _dataOverviewService.GetAvailableYearsAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available years for data overview");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Get day-level aggregated counts and average glucose for a given year
    /// </summary>
    /// <param name="year">The year to aggregate</param>
    /// <param name="dataSource">Optional data source filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet("daily-summary")]
    [RemoteQuery]
    [ProducesResponseType(typeof(DailySummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<DailySummaryResponse>> GetDailySummary(
        [FromQuery] int year,
        [FromQuery] string? dataSource = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (year < 1970 || year > 2100)
                return BadRequest(new { error = "Year must be between 1970 and 2100" });

            var result = await _dataOverviewService.GetDailySummaryAsync(
                year, dataSource, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting daily summary for year {Year}", year);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}

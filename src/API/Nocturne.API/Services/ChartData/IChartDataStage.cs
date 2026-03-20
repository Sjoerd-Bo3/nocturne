namespace Nocturne.API.Services.ChartData;

/// <summary>
/// A single stage in the chart data pipeline.
/// Receives a context, does its work, returns a new context with its contributions.
/// </summary>
public interface IChartDataStage
{
    Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken);
}

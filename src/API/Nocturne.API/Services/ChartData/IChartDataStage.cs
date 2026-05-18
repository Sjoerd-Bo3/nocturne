namespace Nocturne.API.Services.ChartData;

/// <summary>
/// A single stage in the chart data assembly pipeline.
/// Each stage receives a <see cref="ChartDataContext"/>, performs its work
/// (e.g. data fetching, computation, or mapping), and returns an updated context
/// with its contributions added.
/// </summary>
/// <seealso cref="ChartDataContext"/>
/// <seealso cref="IChartDataAssembler"/>
public interface IChartDataStage
{
    /// <summary>
    /// Executes this pipeline stage.
    /// </summary>
    /// <param name="context">The current pipeline context carrying data accumulated by previous stages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An updated <see cref="ChartDataContext"/> with this stage's output appended.</returns>
    Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken);
}

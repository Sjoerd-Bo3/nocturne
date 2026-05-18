using Nocturne.Core.Models;

namespace Nocturne.API.Services.ChartData;

/// <summary>
/// Assembles a final <see cref="DashboardChartData"/> DTO from a fully-populated
/// <see cref="ChartDataContext"/> after all pipeline stages have run.
/// </summary>
/// <seealso cref="ChartDataContext"/>
/// <seealso cref="IChartDataStage"/>
public interface IChartDataAssembler
{
    /// <summary>
    /// Produces the chart data DTO by reading fields from the completed <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The populated pipeline context containing all computed series and markers.</param>
    /// <returns>A fully assembled <see cref="DashboardChartData"/> ready for serialisation.</returns>
    DashboardChartData Assemble(ChartDataContext context);
}

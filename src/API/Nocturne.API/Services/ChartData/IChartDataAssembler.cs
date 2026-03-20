using Nocturne.Core.Models;

namespace Nocturne.API.Services.ChartData;

/// <summary>
/// Assembles the final DashboardChartData DTO from a fully-populated ChartDataContext.
/// </summary>
public interface IChartDataAssembler
{
    DashboardChartData Assemble(ChartDataContext context);
}

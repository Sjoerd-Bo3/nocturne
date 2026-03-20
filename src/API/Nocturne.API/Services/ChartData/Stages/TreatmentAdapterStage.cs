using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.ChartData.Stages;

internal sealed class TreatmentAdapterStage(ITreatmentFoodService treatmentFoodService) : IChartDataStage
{
    public async Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken)
    {
        var carbIntakeIds = context.CarbIntakeList.Select(c => c.Id).ToList();

        var allTreatmentFoods = await treatmentFoodService.GetByCarbIntakeIdsAsync(carbIntakeIds, cancellationToken);

        var foodsByCarbIntake = allTreatmentFoods
            .GroupBy(f => f.CarbIntakeId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var syntheticTreatments = ChartDataService.BuildTreatmentsFromV4Data(
            context.BolusList.ToList(),
            context.CarbIntakeList.ToList(),
            foodsByCarbIntake
        );

        return context with
        {
            SyntheticTreatments = syntheticTreatments,
            FoodsByCarbIntake = foodsByCarbIntake,
        };
    }
}

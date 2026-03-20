using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.ChartData.Stages;

/// <summary>
/// Pipeline stage that loads profile data and derives configuration values used throughout
/// the chart data pipeline: timezone, glucose thresholds, and default basal rate.
/// </summary>
internal sealed class ProfileLoadStage(
    IProfileDataService profileDataService,
    IProfileService profileService,
    ILogger<ProfileLoadStage> logger
) : IChartDataStage
{
    private const double DefaultVeryLow = 54;
    private const double DefaultVeryHigh = 250;

    public async Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken)
    {
        var profiles = await profileDataService.GetProfilesAsync(count: 100, cancellationToken: cancellationToken);
        var profileList = profiles?.ToList() ?? new List<Profile>();

        if (profileList.Count > 0)
        {
            profileService.LoadData(profileList);
            logger.LogDebug("Loaded {Count} profiles into profile service", profileList.Count);
        }

        var timezone = profileService.HasData() ? profileService.GetTimezone() : null;

        ChartThresholdsDto thresholds;
        double defaultBasalRate;

        if (profileService.HasData())
        {
            thresholds = new ChartThresholdsDto
            {
                VeryLow = DefaultVeryLow,
                Low = profileService.GetLowBGTarget(context.EndTime, null),
                High = profileService.GetHighBGTarget(context.EndTime, null),
                VeryHigh = DefaultVeryHigh,
            };
            defaultBasalRate = profileService.GetBasalRate(context.EndTime, null);
        }
        else
        {
            thresholds = new ChartThresholdsDto
            {
                VeryLow = DefaultVeryLow,
                Low = 70,
                High = 180,
                VeryHigh = DefaultVeryHigh,
            };
            defaultBasalRate = 1.0;
        }

        return context with
        {
            Timezone = timezone,
            Thresholds = thresholds,
            DefaultBasalRate = defaultBasalRate,
        };
    }
}

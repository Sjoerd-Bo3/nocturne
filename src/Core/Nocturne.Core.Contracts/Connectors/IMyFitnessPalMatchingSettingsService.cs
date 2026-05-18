using Nocturne.Core.Models.Configuration;

namespace Nocturne.Core.Contracts.Connectors;

/// <summary>
/// Service interface for managing global MyFitnessPal matching settings.
/// </summary>
public interface IMyFitnessPalMatchingSettingsService
{
    /// <summary>
    /// Get current global MyFitnessPal matching settings.
    /// </summary>
    Task<MyFitnessPalMatchingSettings> GetSettingsAsync(
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Save global MyFitnessPal matching settings.
    /// </summary>
    Task<MyFitnessPalMatchingSettings> SaveSettingsAsync(
        MyFitnessPalMatchingSettings settings,
        CancellationToken cancellationToken = default
    );
}

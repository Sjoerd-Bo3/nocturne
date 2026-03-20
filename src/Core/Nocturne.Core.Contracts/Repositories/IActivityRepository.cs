using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for Activity domain operations
/// </summary>
public interface IActivityRepository
{
    /// <summary>
    /// Get activities with pagination
    /// </summary>
    /// <param name="count">Maximum number of activities to return</param>
    /// <param name="skip">Number of activities to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of activities</returns>
    Task<IEnumerable<Activity>> GetActivityAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get activities with pagination (alternative method)
    /// </summary>
    /// <param name="count">Maximum number of activities to return</param>
    /// <param name="skip">Number of activities to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of activities</returns>
    Task<IEnumerable<Activity>> GetActivitiesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get an activity by its ID
    /// </summary>
    /// <param name="id">The activity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The activity if found, null otherwise</returns>
    Task<Activity?> GetActivityByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get activities with advanced filtering options
    /// </summary>
    /// <param name="count">Maximum number of activities to return</param>
    /// <param name="skip">Number of activities to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of activities matching the filter</returns>
    Task<IEnumerable<Activity>> GetActivityWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create multiple activities
    /// </summary>
    /// <param name="activities">The activities to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created activities</returns>
    Task<IEnumerable<Activity>> CreateActivityAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create multiple activities (alternative method)
    /// </summary>
    /// <param name="activities">The activities to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created activities</returns>
    Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an activity by ID
    /// </summary>
    /// <param name="id">The activity ID</param>
    /// <param name="activity">The updated activity data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated activity, or null if not found</returns>
    Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete an activity by ID
    /// </summary>
    /// <param name="id">The activity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteActivityAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count activities with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of activities matching the filter</returns>
    Task<long> CountActivitiesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );
}

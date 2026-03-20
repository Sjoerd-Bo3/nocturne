using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for Profile domain operations
/// </summary>
public interface IProfileRepository
{
    /// <summary>
    /// Get the current active profile
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current profile, or null if no profiles exist</returns>
    Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the profile active at a specific timestamp
    /// </summary>
    /// <param name="timestamp">Unix timestamp in milliseconds</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The profile active at the timestamp if found, null otherwise</returns>
    Task<Profile?> GetProfileAtTimestampAsync(
        long timestamp,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a profile by its ID
    /// </summary>
    /// <param name="id">The profile ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The profile if found, null otherwise</returns>
    Task<Profile?> GetProfileByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get profiles with pagination
    /// </summary>
    /// <param name="count">Maximum number of profiles to return</param>
    /// <param name="skip">Number of profiles to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of profiles</returns>
    Task<IEnumerable<Profile>> GetProfilesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get profiles with advanced filtering options
    /// </summary>
    /// <param name="count">Maximum number of profiles to return</param>
    /// <param name="skip">Number of profiles to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of profiles matching the filter</returns>
    Task<IEnumerable<Profile>> GetProfilesWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create multiple profiles
    /// </summary>
    /// <param name="profiles">The profiles to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created profiles</returns>
    Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update a profile by ID
    /// </summary>
    /// <param name="id">The profile ID</param>
    /// <param name="profile">The updated profile data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated profile, or null if not found</returns>
    Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a profile by ID
    /// </summary>
    /// <param name="id">The profile ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count profiles with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of profiles matching the filter</returns>
    Task<long> CountProfilesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );
}

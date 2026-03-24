using System.Text.Json;
using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for Treatment domain operations
/// </summary>
public interface ITreatmentRepository
{
    /// <summary>
    /// Get a treatment by its ID
    /// </summary>
    /// <param name="id">The treatment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The treatment if found, null otherwise</returns>
    Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get the latest treatment timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The latest timestamp, or null if no treatments exist</returns>
    Task<DateTime?> GetLatestTreatmentTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get the oldest treatment timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The oldest timestamp, or null if no treatments exist</returns>
    Task<DateTime?> GetOldestTreatmentTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Check for duplicate treatment in the database by ID or OriginalId
    /// </summary>
    /// <param name="id">Treatment ID (GUID)</param>
    /// <param name="originalId">Original treatment ID (MongoDB ObjectId)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Existing treatment if duplicate found, null otherwise</returns>
    Task<Treatment?> CheckForDuplicateTreatmentAsync(
        string? id,
        string? originalId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments with pagination
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments within a time range (by mills)
    /// </summary>
    /// <param name="startMills">Start time in milliseconds since Unix epoch</param>
    /// <param name="endMills">End time in milliseconds since Unix epoch</param>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments in the time range</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsByTimeRangeAsync(
        long startMills,
        long endMills,
        int count = 10000,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments with advanced filtering options
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments matching the filter</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments with advanced filtering options including event type filter
    /// </summary>
    /// <param name="eventType">Optional filter by event type</param>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments matching the filter</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        string? eventType,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create a single treatment
    /// </summary>
    /// <param name="treatment">The treatment to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created treatment, or null if creation failed</returns>
    Task<Treatment?> CreateTreatmentAsync(
        Treatment treatment,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create multiple treatments
    /// </summary>
    /// <param name="treatments">The treatments to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created treatments</returns>
    Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update a treatment by ID
    /// </summary>
    /// <param name="id">The treatment ID</param>
    /// <param name="treatment">The updated treatment data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated treatment, or null if not found</returns>
    Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a treatment by ID
    /// </summary>
    /// <param name="id">The treatment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteTreatmentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete treatments using query filters
    /// </summary>
    /// <param name="findQuery">Query filter for treatments to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments deleted</returns>
    Task<long> BulkDeleteTreatmentsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete all treatments with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source to filter by (e.g., "demo-service", "dexcom-connector")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments deleted</returns>
    Task<long> DeleteTreatmentsByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count treatments with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments matching the filter</returns>
    Task<long> CountTreatmentsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments modified since a given timestamp (for incremental sync)
    /// </summary>
    Task<IEnumerable<Treatment>> GetTreatmentsModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Patch a treatment by ID using JSON merge-patch semantics
    /// </summary>
    Task<Treatment?> PatchTreatmentAsync(
        string id,
        JsonElement patchData,
        CancellationToken cancellationToken = default
    );

}

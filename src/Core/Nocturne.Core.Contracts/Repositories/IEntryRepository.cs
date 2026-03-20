using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Repositories;

/// <summary>
/// Repository port for Entry domain operations
/// </summary>
public interface IEntryRepository
{
    /// <summary>
    /// Get the most recent entry
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current entry, or null if no entries exist</returns>
    Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the latest entry timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The latest timestamp, or null if no entries exist</returns>
    Task<DateTime?> GetLatestEntryTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get the oldest entry timestamp for a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The oldest timestamp, or null if no entries exist</returns>
    Task<DateTime?> GetOldestEntryTimestampBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get an entry by its ID
    /// </summary>
    /// <param name="id">The entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entry if found, null otherwise</returns>
    Task<Entry?> GetEntryByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entries with optional filtering and pagination
    /// </summary>
    /// <param name="type">Optional entry type filter</param>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="skip">Number of entries to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of entries</returns>
    Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get entries with advanced filtering options
    /// </summary>
    /// <param name="type">Optional entry type filter</param>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="skip">Number of entries to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="dateString">Optional date filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of entries matching the filter</returns>
    Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? dateString = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Check for duplicate entries in the database within a time window
    /// </summary>
    /// <param name="device">Device identifier</param>
    /// <param name="type">Entry type (e.g., "sgv", "mbg", "cal")</param>
    /// <param name="sgv">Sensor glucose value in mg/dL</param>
    /// <param name="mills">Timestamp in milliseconds since Unix epoch</param>
    /// <param name="windowMinutes">Time window in minutes to check for duplicates (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Existing entry if duplicate found, null otherwise</returns>
    Task<Entry?> CheckForDuplicateEntryAsync(
        string? device,
        string type,
        double? sgv,
        long mills,
        int windowMinutes = 5,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create a single entry
    /// </summary>
    /// <param name="entry">The entry to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created entry, or null if creation failed</returns>
    Task<Entry?> CreateEntryAsync(Entry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create multiple entries
    /// </summary>
    /// <param name="entries">The entries to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of created entries</returns>
    Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an entry by ID
    /// </summary>
    /// <param name="id">The entry ID</param>
    /// <param name="entry">The updated entry data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The updated entry, or null if not found</returns>
    Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete an entry by ID
    /// </summary>
    /// <param name="id">The entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteEntryAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete all entries with the specified data source
    /// </summary>
    /// <param name="dataSource">The data source to filter by (e.g., "demo-service", "dexcom-connector")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries deleted</returns>
    Task<long> DeleteEntriesByDataSourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Bulk delete entries using query filters
    /// </summary>
    /// <param name="findQuery">Query filter for entries to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries deleted</returns>
    Task<long> BulkDeleteEntriesAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count entries with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="type">Optional entry type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries matching the filter</returns>
    Task<long> CountEntriesAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get entries modified since a given timestamp (for incremental sync)
    /// </summary>
    Task<IEnumerable<Entry>> GetEntriesModifiedSinceAsync(
        long lastModifiedMills,
        int limit = 500,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get statistics for entries from a specific data source
    /// </summary>
    /// <param name="dataSource">The data source name</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Data source statistics</returns>
    Task<DataSourceStats> GetEntryStatsBySourceAsync(
        string dataSource,
        CancellationToken cancellationToken = default
    );
}

using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Infrastructure;

/// <summary>
/// Service for tracking async processing status
/// </summary>
public interface IProcessingStatusService
{
    /// <summary>
    /// Gets the processing status for a correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation ID to look up</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing status or null if not found</returns>
    Task<ProcessingStatus?> GetStatusAsync(
        string correlationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates the processing status for a correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="status">The updated status</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateStatusAsync(
        string correlationId,
        ProcessingStatus status,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Marks processing as completed with optional results
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="results">Optional processing results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkCompletedAsync(
        string correlationId,
        object? results = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Marks processing as failed with error messages
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="errors">Error messages</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task MarkFailedAsync(
        string correlationId,
        IEnumerable<string> errors,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Initializes processing status for a new correlation ID
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="totalCount">Total number of items to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task InitializeAsync(
        string correlationId,
        int totalCount,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Updates processing progress
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="processedCount">Number of items processed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateProgressAsync(
        string correlationId,
        int processedCount,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Waits for processing to complete with a timeout
    /// </summary>
    /// <param name="correlationId">The correlation ID</param>
    /// <param name="timeout">Maximum time to wait</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Final processing status or null if timeout</returns>
    Task<ProcessingStatus?> WaitForCompletionAsync(
        string correlationId,
        TimeSpan timeout,
        CancellationToken cancellationToken = default
    );
}

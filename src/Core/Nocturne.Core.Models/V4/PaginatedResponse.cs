namespace Nocturne.Core.Models.V4;

/// <summary>
/// Generic paginated API response wrapper used by all V4 collection endpoints.
/// </summary>
/// <typeparam name="T">The type of items contained in <see cref="Data"/>.</typeparam>
/// <seealso cref="PaginationInfo"/>
public class PaginatedResponse<T>
{
    /// <summary>
    /// The page of items returned for the current request.
    /// </summary>
    public IEnumerable<T> Data { get; set; } = [];

    /// <summary>
    /// Pagination metadata describing the current window and total result count.
    /// </summary>
    public PaginationInfo Pagination { get; set; } = new();
}

/// <summary>
/// Pagination metadata for a <see cref="PaginatedResponse{T}"/>.
/// </summary>
/// <param name="Limit">Maximum number of records returned per page (default 100).</param>
/// <param name="Offset">Zero-based index of the first record in the current page (default 0).</param>
/// <param name="Total">Total number of records matching the query across all pages.</param>
public record PaginationInfo(int Limit = 100, int Offset = 0, int Total = 0);

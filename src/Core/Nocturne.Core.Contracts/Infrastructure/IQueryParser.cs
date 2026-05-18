using System.Linq.Expressions;

namespace Nocturne.Core.Contracts.Infrastructure;

/// <summary>
/// Service for parsing Nightscout-style queries for Entity Framework Core compatibility
/// </summary>
public interface IQueryParser
{
    /// <summary>
    /// Apply Nightscout-style query parameters to an IQueryable
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="queryable">Base queryable to filter</param>
    /// <param name="findQuery">Nightscout find query string (URL-decoded)</param>
    /// <param name="options">Query parsing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Filtered queryable</returns>
    Task<IQueryable<T>> ApplyQueryAsync<T>(
        IQueryable<T> queryable, 
        string findQuery, 
        QueryOptions options,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Parse Nightscout-style find query into Entity Framework expressions
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="findQuery">Nightscout find query string</param>
    /// <param name="options">Query parsing options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Expression predicate for filtering</returns>
    Task<Expression<Func<T, bool>>?> ParseFilterAsync<T>(
        string findQuery, 
        QueryOptions options,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Apply automatic date constraints if no date filters are present
    /// </summary>
    /// <typeparam name="T">Entity type</typeparam>
    /// <param name="queryable">Base queryable</param>
    /// <param name="findQuery">Nightscout find query string</param>
    /// <param name="dateString">Date string parameter</param>
    /// <param name="options">Query parsing options</param>
    /// <returns>Queryable with date constraints applied</returns>
    IQueryable<T> ApplyDefaultDateFilter<T>(
        IQueryable<T> queryable, 
        string? findQuery, 
        string? dateString, 
        QueryOptions options) where T : class;
}

/// <summary>
/// Options for Nightscout query parsing
/// </summary>
public class QueryOptions
{
    /// <summary>
    /// Default date range to apply when no date constraints are present
    /// </summary>
    public TimeSpan DefaultDateRange { get; set; } = TimeSpan.FromDays(4); // 2 * TWO_DAYS from Nightscout

    /// <summary>
    /// Name of the date field to use for date constraints
    /// </summary>
    public string DateField { get; set; } = "Mills";

    /// <summary>
    /// Whether to disable automatic date filtering
    /// </summary>
    public bool DisableDefaultDateFilter { get; set; } = false;

    /// <summary>
    /// Whether dates are stored as Unix epoch milliseconds
    /// </summary>
    public bool UseEpochDates { get; set; } = true;

    /// <summary>
    /// Type converters for different fields
    /// </summary>
    public Dictionary<string, Func<string, object>> TypeConverters { get; set; } = new();

    /// <summary>
    /// Maximum allowed date range in days to prevent excessive queries
    /// </summary>
    public int MaxDateRangeDays { get; set; } = 180;
}
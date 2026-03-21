using System.Text.Json;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Implementation of time-based queries and advanced data slicing
/// Replicates legacy JavaScript functionality for time pattern matching
/// </summary>
public class TimeQueryService : ITimeQueryService
{
    private readonly IEntryRepository _entries;
    private readonly ITreatmentRepository _treatments;
    private readonly IDeviceStatusRepository _deviceStatuses;
    private readonly IBraceExpansionService _braceExpansionService;
    private readonly ILogger<TimeQueryService> _logger;

    public TimeQueryService(
        IEntryRepository entries,
        ITreatmentRepository treatments,
        IDeviceStatusRepository deviceStatuses,
        IBraceExpansionService braceExpansionService,
        ILogger<TimeQueryService> logger
    )
    {
        _entries = entries;
        _treatments = treatments;
        _deviceStatuses = deviceStatuses;
        _braceExpansionService = braceExpansionService;
        _logger = logger;
    }

    /// <summary>
    /// Execute a time-based query with pattern matching
    /// </summary>
    public async Task<IEnumerable<Entry>> ExecuteTimeQueryAsync(
        string? prefix,
        string? regex,
        string storage = "entries",
        string fieldName = "dateString",
        Dictionary<string, object>? queryParameters = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Executing time query - prefix: {Prefix}, regex: {Regex}, storage: {Storage}, field: {Field}",
            prefix,
            regex,
            storage,
            fieldName
        );

        // Prepare time patterns for the specified field
        var timePatterns = _braceExpansionService.PrepareTimePatterns(prefix, regex, fieldName);

        // Build query parameters for PostgreSQL
        var queryParams = new Dictionary<string, object>();

        // Add time pattern filtering
        if (
            timePatterns.CanOptimizeWithIndex
            && !string.IsNullOrEmpty(timePatterns.SingleRegexPattern)
        )
        {
            queryParams[fieldName + "_regex"] = timePatterns.SingleRegexPattern;
        }
        else if (timePatterns.InPatterns?.Any() == true)
        {
            queryParams[fieldName + "_in"] = timePatterns.InPatterns;
        }

        // Apply additional query parameters
        if (queryParameters != null)
        {
            foreach (var kvp in queryParameters)
            {
                // Handle special MongoDB-style query operators
                if (kvp.Key == "find" && kvp.Value is Dictionary<string, object> findDict)
                {
                    foreach (var findKvp in findDict)
                    {
                        if (findKvp.Value is Dictionary<string, object> operatorDict)
                        {
                            // Handle MongoDB operators like $in, $nin, $regex, etc.
                            foreach (var opKvp in operatorDict)
                            {
                                switch (opKvp.Key)
                                {
                                    case "$in":
                                        queryParams[findKvp.Key + "_in"] = ConvertArray(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$nin":
                                        queryParams[findKvp.Key + "_nin"] = ConvertArray(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$regex":
                                        queryParams[findKvp.Key + "_regex"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$gte":
                                        queryParams[findKvp.Key + "_gte"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$lte":
                                        queryParams[findKvp.Key + "_lte"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$gt":
                                        queryParams[findKvp.Key + "_gt"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$lt":
                                        queryParams[findKvp.Key + "_lt"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    case "$ne":
                                        queryParams[findKvp.Key + "_ne"] = ConvertValue(
                                            opKvp.Value
                                        );
                                        break;
                                    default:
                                        queryParams[findKvp.Key] = ConvertValue(opKvp.Value);
                                        break;
                                }
                            }
                        }
                        else
                        {
                            queryParams[findKvp.Key] = ConvertValue(findKvp.Value);
                        }
                    }
                }
                else if (kvp.Key != "count" && kvp.Key != "sort")
                {
                    queryParams[kvp.Key] = ConvertValue(kvp.Value);
                }
            }
        }

        // Execute query based on storage type
        var findQuery = ConvertQueryParamsToFindQuery(queryParams);

        return storage.ToLowerInvariant() switch
        {
            "entries" => await _entries.GetEntriesWithAdvancedFilterAsync(
                count: 1000, // Default limit
                skip: 0,
                findQuery: findQuery,
                cancellationToken: cancellationToken
            ),
            "treatments" => (
                await _treatments.GetTreatmentsWithAdvancedFilterAsync(
                    count: 1000,
                    skip: 0,
                    findQuery: findQuery,
                    cancellationToken: cancellationToken
                )
            ).Select(t => ConvertTreatmentToEntry(t)),
            "devicestatus" => (
                await _deviceStatuses.GetDeviceStatusWithAdvancedFilterAsync(
                    count: 1000,
                    skip: 0,
                    findQuery: findQuery,
                    cancellationToken: cancellationToken
                )
            ).Select(ds => ConvertDeviceStatusToEntry(ds)),
            _ => throw new ArgumentException($"Unsupported storage type: {storage}"),
        };
    }

    /// <summary>
    /// Execute an advanced slice query with field and type filtering
    /// </summary>
    public async Task<IEnumerable<Entry>> ExecuteSliceQueryAsync(
        string storage,
        string field,
        string? type,
        string? prefix,
        string? regex,
        Dictionary<string, object>? queryParameters = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Executing slice query - storage: {Storage}, field: {Field}, type: {Type}, prefix: {Prefix}, regex: {Regex}",
            storage,
            field,
            type,
            prefix,
            regex
        );

        // Prepare time patterns for the specified field
        var timePatterns = _braceExpansionService.PrepareTimePatterns(prefix, regex, field);

        // Build query parameters for PostgreSQL
        var queryParams = new Dictionary<string, object>();

        // Add time pattern filtering
        if (
            timePatterns.CanOptimizeWithIndex
            && !string.IsNullOrEmpty(timePatterns.SingleRegexPattern)
        )
        {
            queryParams[field + "_regex"] = timePatterns.SingleRegexPattern;
        }
        else
        {
            queryParams[field + "_in"] = timePatterns.InPatterns;
        }

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(type))
        {
            queryParams["type"] = type;
        }

        // Apply additional query parameters
        if (queryParameters != null)
        {
            foreach (var kvp in queryParameters)
            {
                queryParams[kvp.Key] = kvp.Value;
            }
        }

        // Execute query based on storage type
        // Note: PostgreSQL service uses different method signatures than MongoDB
        // Converting parameters to match PostgreSQL API
        var findQuery = ConvertQueryParamsToFindQuery(queryParams);

        return storage.ToLowerInvariant() switch
        {
            "entries" => await _entries.GetEntriesWithAdvancedFilterAsync(
                count: 1000, // Default limit
                skip: 0,
                findQuery: findQuery,
                cancellationToken: cancellationToken
            ),
            "treatments" => (
                await _treatments.GetTreatmentsWithAdvancedFilterAsync(
                    count: 1000,
                    skip: 0,
                    findQuery: findQuery,
                    cancellationToken: cancellationToken
                )
            ).Select(t => ConvertTreatmentToEntry(t)),
            "devicestatus" => (
                await _deviceStatuses.GetDeviceStatusWithAdvancedFilterAsync(
                    count: 1000,
                    skip: 0,
                    findQuery: findQuery,
                    cancellationToken: cancellationToken
                )
            ).Select(ds => ConvertDeviceStatusToEntry(ds)),
            _ => throw new ArgumentException($"Unsupported storage type: {storage}"),
        };
    }

    /// <summary>
    /// Generate debug information for time pattern queries
    /// </summary>
    public TimeQueryEcho GenerateTimeQueryEcho(
        string? prefix,
        string? regex,
        string storage = "entries",
        string fieldName = "dateString",
        Dictionary<string, object>? queryParameters = null
    )
    {
        _logger.LogDebug(
            "Generating time query echo - prefix: {Prefix}, regex: {Regex}, storage: {Storage}, field: {Field}",
            prefix,
            regex,
            storage,
            fieldName
        );

        // Prepare time patterns
        var timePatterns = _braceExpansionService.PrepareTimePatterns(prefix, regex, fieldName);

        // Build the query structure that matches legacy MongoDB format for compatibility
        var queryStructure = new Dictionary<string, object>();

        if (
            timePatterns.CanOptimizeWithIndex
            && !string.IsNullOrEmpty(timePatterns.SingleRegexPattern)
        )
        {
            // Use MongoDB-style $regex operator for compatibility
            queryStructure[fieldName] = new Dictionary<string, object>
            {
                ["$regex"] = timePatterns.SingleRegexPattern,
            };
        }
        else if (timePatterns.InPatterns?.Any() == true)
        {
            // Use MongoDB-style $in operator for compatibility
            queryStructure[fieldName] = new Dictionary<string, object>
            {
                ["$in"] = timePatterns.InPatterns,
            };
        }

        // Add any additional query parameters
        if (queryParameters != null)
        {
            foreach (var kvp in queryParameters)
            {
                if (kvp.Key != "find" && kvp.Key != "count" && kvp.Key != "sort")
                {
                    queryStructure[kvp.Key] = kvp.Value;
                }
            }

            // Handle find parameters
            if (
                queryParameters.TryGetValue("find", out var findValue)
                && findValue is Dictionary<string, object> findDict
            )
            {
                foreach (var findKvp in findDict)
                {
                    queryStructure[findKvp.Key] = findKvp.Value;
                }
            }
        }

        return new TimeQueryEcho
        {
            Req = new TimeQueryRequest
            {
                Params = new Dictionary<string, string?>
                {
                    ["prefix"] = prefix,
                    ["regex"] = regex,
                    ["storage"] = storage,
                    ["field"] = fieldName,
                },
                Query = queryParameters ?? new Dictionary<string, object>(),
            },
            Pattern = timePatterns.Patterns,
            Query = queryStructure,
        };
    }

    /// <summary>
    /// Convert a value to the appropriate type for MongoDB queries
    /// </summary>
    private object ConvertValue(object value)
    {
        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Number => jsonElement.TryGetInt64(out var longVal)
                    ? longVal
                    : jsonElement.GetDouble(),
                JsonValueKind.String => jsonElement.GetString() ?? "",
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => value.ToString() ?? "",
            };
        }

        return value;
    }

    /// <summary>
    /// Convert a value to an array for MongoDB $in/$nin operations
    /// </summary>
    private IEnumerable<object> ConvertArray(object value)
    {
        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.EnumerateArray().Select(element => ConvertValue(element)).ToArray();
        }

        if (value is IEnumerable<object> enumerable)
        {
            return enumerable.Select(item => ConvertValue(item)).ToArray();
        }

        return new[] { ConvertValue(value) };
    }

    /// <summary>
    /// Convert query parameters dictionary to PostgreSQL find query string
    /// </summary>
    private string? ConvertQueryParamsToFindQuery(Dictionary<string, object> queryParams)
    {
        if (queryParams.Count == 0)
        {
            return null;
        }

        // For now, convert to a simple JSON representation
        // In a full implementation, this would translate to proper SQL WHERE clauses
        return JsonSerializer.Serialize(queryParams);
    }

    /// <summary>
    /// Convert Treatment to Entry for unified result handling
    /// </summary>
    private Entry ConvertTreatmentToEntry(Treatment treatment)
    {
        return new Entry
        {
            Id = treatment.Id,
            Type = "treatment",
            Mills = treatment.Mills,
            CreatedAt = treatment.CreatedAt,
            UtcOffset = treatment.UtcOffset,
            // Map other relevant fields as needed
        };
    }

    /// <summary>
    /// Convert DeviceStatus to Entry for unified result handling
    /// </summary>
    private Entry ConvertDeviceStatusToEntry(DeviceStatus deviceStatus)
    {
        return new Entry
        {
            Id = deviceStatus.Id,
            Type = "devicestatus",
            Mills = deviceStatus.Mills,
            CreatedAt = deviceStatus.CreatedAt,
            UtcOffset = deviceStatus.UtcOffset,
            Device = deviceStatus.Device,
            // Map other relevant fields as needed
        };
    }
}

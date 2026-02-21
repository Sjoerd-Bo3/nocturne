using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Services;

/// <summary>
/// Response for GET /api/v4/data-overview/years
/// </summary>
public class DataOverviewYearsResponse
{
    [JsonPropertyName("years")]
    public int[] Years { get; set; } = [];

    [JsonPropertyName("availableDataSources")]
    public string[] AvailableDataSources { get; set; } = [];
}

/// <summary>
/// Response for GET /api/v4/data-overview/daily-summary
/// </summary>
public class DailySummaryResponse
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("dataSources")]
    public string[]? DataSources { get; set; }

    [JsonPropertyName("days")]
    public DailySummaryDay[] Days { get; set; } = [];
}

/// <summary>
/// Aggregated data for a single day
/// </summary>
public class DailySummaryDay
{
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;

    [JsonPropertyName("averageGlucoseMgdl")]
    public double? AverageGlucoseMgdl { get; set; }

    [JsonPropertyName("totalBolusUnits")]
    public double? TotalBolusUnits { get; set; }

    [JsonPropertyName("totalBasalUnits")]
    public double? TotalBasalUnits { get; set; }

    [JsonPropertyName("totalDailyDose")]
    public double? TotalDailyDose { get; set; }

    [JsonPropertyName("totalCarbs")]
    public double? TotalCarbs { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }

    /// <summary>
    /// Record counts keyed by SyncDataType name (e.g., "Glucose", "Boluses", "StateSpans")
    /// </summary>
    [JsonPropertyName("counts")]
    public Dictionary<string, int> Counts { get; set; } = new();
}

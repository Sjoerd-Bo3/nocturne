using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Nocturne.Tools.McpServer.Tools;

/// <summary>
/// MCP tools for querying v4 profile and therapy settings.
/// </summary>
[McpServerToolType]
public static class ProfileTools
{
    [McpServerTool]
    [Description("Get the consolidated profile summary including all therapy settings, basal schedules, carb ratios, sensitivity factors, and target ranges.")]
    public static async Task<string> GetProfileSummary()
    {
        try
        {
            var json = await ToolBase.Api.GetAsync("api/v4/profile/summary");
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving profile summary: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get therapy settings (core pump configuration: insulin action time, max bolus, etc.).")]
    public static async Task<string> GetTherapySettings(
        [Description("Maximum number of settings records (default: 10)")] int limit = 10
    )
    {
        try
        {
            var endpoint = ToolBase.BuildQuery("api/v4/profile/settings", new()
            {
                ["limit"] = limit.ToString(),
                ["sort"] = "timestamp_desc",
            });

            var json = await ToolBase.Api.GetAsync(endpoint);
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving therapy settings: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get basal rate schedules for a specific profile name.")]
    public static async Task<string> GetBasalSchedule(
        [Description("Profile name (e.g. 'Default')")] string profileName
    )
    {
        try
        {
            var json = await ToolBase.Api.GetAsync($"api/v4/profile/basal/{Uri.EscapeDataString(profileName)}");
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving basal schedule: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get carb ratio schedules for a specific profile name.")]
    public static async Task<string> GetCarbRatioSchedule(
        [Description("Profile name (e.g. 'Default')")] string profileName
    )
    {
        try
        {
            var json = await ToolBase.Api.GetAsync($"api/v4/profile/carb-ratio/{Uri.EscapeDataString(profileName)}");
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving carb ratio schedule: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get insulin sensitivity factor schedules for a specific profile name.")]
    public static async Task<string> GetSensitivitySchedule(
        [Description("Profile name (e.g. 'Default')")] string profileName
    )
    {
        try
        {
            var json = await ToolBase.Api.GetAsync($"api/v4/profile/sensitivity/{Uri.EscapeDataString(profileName)}");
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving sensitivity schedule: {ex.Message}";
        }
    }
}

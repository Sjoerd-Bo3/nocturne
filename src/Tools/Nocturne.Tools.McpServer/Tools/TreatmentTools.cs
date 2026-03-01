using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Nocturne.Tools.McpServer.Tools;

/// <summary>
/// MCP tools for querying v4 treatments (corrections, carb treatments, etc.).
/// Note: V4 treatments do NOT include basal data — use StateSpanTools for that.
/// </summary>
[McpServerToolType]
public static class TreatmentTools
{
    [McpServerTool]
    [Description("Get recent treatments (corrections, carb treatments, temp targets, etc.). V4 treatments exclude basal data — use GetStateSpans for basal/pump modes.")]
    public static async Task<string> GetRecentTreatments(
        [Description("Number of treatments to return (default: 50)")] int count = 50,
        [Description("Filter by event type (e.g. 'Correction Bolus', 'Carb Correction', 'Temp Basal')")] string? eventType = null
    )
    {
        try
        {
            var endpoint = ToolBase.BuildQuery("api/v4/treatments", new()
            {
                ["count"] = count.ToString(),
                ["eventType"] = eventType,
            });

            var json = await ToolBase.Api.GetAsync(endpoint);
            return json;
        }
        catch (Exception ex)
        {
            return $"Error retrieving treatments: {ex.Message}";
        }
    }

    [McpServerTool]
    [Description("Get a specific treatment by its ID.")]
    public static async Task<string> GetTreatment(
        [Description("The treatment ID")] string id
    )
    {
        try
        {
            var json = await ToolBase.Api.GetAsync($"api/v4/treatments/{id}");
            return json;
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return $"Treatment '{id}' not found";
        }
        catch (Exception ex)
        {
            return $"Error retrieving treatment: {ex.Message}";
        }
    }
}

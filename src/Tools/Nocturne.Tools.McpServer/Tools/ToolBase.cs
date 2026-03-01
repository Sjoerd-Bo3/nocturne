using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.Tools.McpServer.Services;

namespace Nocturne.Tools.McpServer.Tools;

/// <summary>
/// Shared initialization and utilities for MCP tool classes.
/// Each tool class should call <see cref="Initialize"/> once at startup.
/// </summary>
public static class ToolBase
{
    private static IApiService? _apiService;

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static void Initialize(IServiceProvider serviceProvider)
    {
        _apiService = serviceProvider.GetRequiredService<IApiService>();
    }

    internal static IApiService Api =>
        _apiService ?? throw new InvalidOperationException("ToolBase not initialized. Call ToolBase.Initialize first.");

    internal static string ToIso(DateTime dt) => dt.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");

    internal static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOptions);

    internal static string BuildQuery(string basePath, Dictionary<string, string?> parameters)
    {
        var pairs = parameters
            .Where(p => p.Value != null)
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");

        var query = string.Join("&", pairs);
        return string.IsNullOrEmpty(query) ? basePath : $"{basePath}?{query}";
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Devices;

namespace Nocturne.API.Services.Devices;

/// <summary>
/// Configuration options for the oref WASM service registered via <see cref="OrefServiceExtensions.AddOrefService"/>.
/// </summary>
public class OrefServiceOptions
{
    /// <summary>
    /// Path to the oref WASM file.
    /// Defaults to looking for oref.wasm in the application directory.
    /// </summary>
    public string WasmPath { get; set; } = "oref.wasm";

    /// <summary>
    /// Whether to enable the oref WASM service.
    /// When false, the legacy C# IOB/COB services will be used.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// DI registration helpers for the <see cref="OrefWasmService"/>.
/// </summary>
public static class OrefServiceExtensions
{
    /// <summary>
    /// Registers <see cref="OrefWasmService"/> as a singleton <see cref="IOrefService"/>
    /// after resolving the WASM binary path from a prioritised search list.
    /// </summary>
    /// <remarks>
    /// When <see cref="OrefServiceOptions.Enabled"/> is <see langword="false"/> no
    /// <see cref="IOrefService"/> registration is made and the calling code is
    /// responsible for providing an alternative implementation or handling the missing service.
    /// WASM path resolution order: absolute path → current directory → AppContext.BaseDirectory →
    /// relative Rust build output path (for local development) → <c>wasm/</c> subdirectory.
    /// </remarks>
    public static IServiceCollection AddOrefService(
        this IServiceCollection services,
        Action<OrefServiceOptions>? configure = null
    )
    {
        var options = new OrefServiceOptions();
        configure?.Invoke(options);

        // Register options
        services.AddSingleton(options);

        if (!options.Enabled)
        {
            return services;
        }

        // Try to find the WASM file
        var wasmPath = ResolveWasmPath(options.WasmPath);

        // Register as singleton since it holds the WASM instance
        services.AddSingleton<IOrefService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<OrefWasmService>>();
            return new OrefWasmService(logger, wasmPath);
        });

        return services;
    }

    /// <summary>
    /// Resolve the full path to the WASM file.
    /// </summary>
    private static string ResolveWasmPath(string configuredPath)
    {
        // If it's already an absolute path, use it
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        // Try multiple locations
        var searchPaths = new[]
        {
            // Current directory
            Path.Combine(Environment.CurrentDirectory, configuredPath),
            // Application base directory
            Path.Combine(AppContext.BaseDirectory, configuredPath),
            // Relative to the oref source (for development)
            Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "Core",
                "oref",
                "target",
                "wasm32-unknown-unknown",
                "release",
                "oref.wasm"
            ),
            // Look for it in a wasm subdirectory
            Path.Combine(AppContext.BaseDirectory, "wasm", "oref.wasm"),
        };

        foreach (var path in searchPaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        // Return the configured path even if not found (let the service handle the error)
        return configuredPath;
    }
}

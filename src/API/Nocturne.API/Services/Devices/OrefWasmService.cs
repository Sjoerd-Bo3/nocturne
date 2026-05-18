using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.Devices;
using Wasmtime;

namespace Nocturne.API.Services.Devices;

/// <summary>
/// Calls the oref Rust algorithms via a WebAssembly module loaded with Wasmtime, providing
/// high-performance IOB, COB, autosens, and determine-basal calculations. Manages the WASM engine,
/// module, linker, store, and memory lifetime, exposing them via <see cref="IOrefService"/>.
/// </summary>
/// <seealso cref="IOrefService"/>
public class OrefWasmService : IOrefService, IAsyncDisposable, IDisposable
{
    private readonly ILogger<OrefWasmService> _logger;
    private readonly Engine? _engine;
    private readonly Module? _module;
    private readonly Linker? _linker;
    private readonly Store? _store;
    private readonly Instance? _instance;
    private readonly Memory? _memory;

    // WASM function exports
    private readonly Func<int, int, int>? _calculateIob;
    private readonly Func<int, int, int, int>? _calculateCob;
    private readonly Func<int, int, int, int>? _calculateAutosens;
    private readonly Func<int, int>? _determineBasal;
    private readonly Func<int, int>? _calculateGlucoseStatus;
    private readonly Func<int, int>? _alloc;
    private readonly Action<int>? _dealloc;

    private bool _disposed;
    private bool _isAvailable;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string Version => "0.1.0"; // Match Cargo.toml version

    public OrefWasmService(ILogger<OrefWasmService> logger, string wasmPath)
    {
        _logger = logger;
        _isAvailable = false;

        try
        {
            // Create engine with optimized settings for computation
            var engineConfig = new Config()
                .WithOptimizationLevel(OptimizationLevel.Speed)
                .WithCraneliftNaNCanonicalization(true);

            _engine = new Engine(engineConfig);

            if (!File.Exists(wasmPath))
            {
                _logger.LogWarning(
                    "WASM file not found at {Path}. oref calculations will use fallback.",
                    wasmPath
                );
                return;
            }

            // Load the compiled WASM module
            _module = Module.FromFile(_engine, wasmPath);
            _store = new Store(_engine);
            _linker = new Linker(_engine);

            // Link WASI for console/filesystem operations if needed
            _linker.DefineWasi();

            // Instantiate the module
            _instance = _linker.Instantiate(_store, _module);

            // Get memory export
            _memory = _instance.GetMemory("memory");
            if (_memory == null)
            {
                _logger.LogError(
                    "WASM module does not export 'memory'. oref calculations will fail."
                );
                return;
            }

            // Get function exports
            _calculateIob = _instance.GetFunction<int, int, int>("calculate_iob");
            _calculateCob = _instance.GetFunction<int, int, int, int>("calculate_cob");
            _calculateAutosens = _instance.GetFunction<int, int, int, int>("calculate_autosens");
            _determineBasal = _instance.GetFunction<int, int>("determine_basal");
            _calculateGlucoseStatus = _instance.GetFunction<int, int>("calculate_glucose_status");

            // Memory allocation helpers (if exported)
            _alloc = _instance.GetFunction<int, int>("alloc");
            _dealloc = _instance.GetAction<int>("dealloc");

            _isAvailable = true;
            _logger.LogInformation("oref WASM module loaded successfully from {Path}", wasmPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize oref WASM runtime");
        }
    }

    /// <inheritdoc />
    public Task<OrefIobResult[]> CalculateIobAsync(
        OrefProfile profile,
        IEnumerable<OrefTreatment> treatments,
        long? time = null,
        bool currentOnly = true
    )
    {
        if (_calculateIob == null || _memory == null)
        {
            _logger.LogWarning("IOB calculation not available - WASM module not loaded");
            return Task.FromResult(Array.Empty<OrefIobResult>());
        }

        try
        {
            // Prepare input JSON
            var input = new
            {
                profile,
                treatments = treatments.ToArray(),
                time = time ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                currentOnly,
            };
            var inputJson = JsonSerializer.Serialize(input, JsonOptions);

            // Call WASM function
            var resultPtr = CallWasmWithString(inputJson, _calculateIob);

            // Read result string
            var resultJson = ReadWasmString(resultPtr);

            if (string.IsNullOrEmpty(resultJson))
            {
                _logger.LogWarning("IOB calculation returned empty result");
                return Task.FromResult(Array.Empty<OrefIobResult>());
            }

            // Parse result - WASM returns array of IOB entries or single entry
            if (resultJson.StartsWith('['))
            {
                var results = JsonSerializer.Deserialize<OrefIobResult[]>(resultJson, JsonOptions);
                return Task.FromResult(results ?? Array.Empty<OrefIobResult>());
            }
            else
            {
                var result = JsonSerializer.Deserialize<OrefIobResult>(resultJson, JsonOptions);
                return Task.FromResult(result != null ? [result] : Array.Empty<OrefIobResult>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IOB calculation failed");
            return Task.FromResult(Array.Empty<OrefIobResult>());
        }
    }

    /// <inheritdoc />
    public Task<OrefCobResult> CalculateCobAsync(
        OrefProfile profile,
        IEnumerable<OrefGlucoseReading> glucose,
        IEnumerable<OrefTreatment> treatments,
        long? time = null
    )
    {
        if (_calculateCob == null || _memory == null)
        {
            _logger.LogWarning("COB calculation not available - WASM module not loaded");
            return Task.FromResult(new OrefCobResult());
        }

        try
        {
            var input = new
            {
                profile,
                glucose = glucose.ToArray(),
                treatments = treatments.ToArray(),
                time = time ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            var inputJson = JsonSerializer.Serialize(input, JsonOptions);

            // Using a wrapper to handle the 3-parameter function
            var combinedPtr = WriteWasmString(inputJson);
            var resultPtr = _calculateCob(combinedPtr, 0, 0); // Single combined input

            var resultJson = ReadWasmString(resultPtr);
            var result = JsonSerializer.Deserialize<OrefCobResult>(resultJson, JsonOptions);

            return Task.FromResult(result ?? new OrefCobResult());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "COB calculation failed");
            return Task.FromResult(new OrefCobResult());
        }
    }

    /// <inheritdoc />
    public Task<OrefAutosensResult> CalculateAutosensAsync(
        OrefProfile profile,
        IEnumerable<OrefGlucoseReading> glucose,
        IEnumerable<OrefTreatment> treatments,
        long? time = null
    )
    {
        if (_calculateAutosens == null || _memory == null)
        {
            _logger.LogWarning("Autosens calculation not available - WASM module not loaded");
            return Task.FromResult(new OrefAutosensResult { Ratio = 1.0 });
        }

        try
        {
            var input = new
            {
                profile,
                glucose = glucose.ToArray(),
                treatments = treatments.ToArray(),
                time = time ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };
            var inputJson = JsonSerializer.Serialize(input, JsonOptions);

            var combinedPtr = WriteWasmString(inputJson);
            var resultPtr = _calculateAutosens(combinedPtr, 0, 0);

            var resultJson = ReadWasmString(resultPtr);
            var result = JsonSerializer.Deserialize<OrefAutosensResult>(resultJson, JsonOptions);

            return Task.FromResult(result ?? new OrefAutosensResult { Ratio = 1.0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Autosens calculation failed");
            return Task.FromResult(new OrefAutosensResult { Ratio = 1.0 });
        }
    }

    /// <inheritdoc />
    public Task<OrefDetermineBasalResult> DetermineBasalAsync(OrefDetermineBasalInputs inputs)
    {
        if (_determineBasal == null || _memory == null)
        {
            _logger.LogWarning("Determine basal not available - WASM module not loaded");
            return Task.FromResult(
                new OrefDetermineBasalResult { Error = "WASM module not loaded" }
            );
        }

        try
        {
            var inputJson = JsonSerializer.Serialize(inputs, JsonOptions);
            var resultPtr = CallWasmWithString(inputJson, _determineBasal);

            var resultJson = ReadWasmString(resultPtr);
            var result = JsonSerializer.Deserialize<OrefDetermineBasalResult>(
                resultJson,
                JsonOptions
            );

            return Task.FromResult(
                result ?? new OrefDetermineBasalResult { Error = "Deserialization failed" }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Determine basal calculation failed");
            return Task.FromResult(new OrefDetermineBasalResult { Error = ex.Message });
        }
    }

    /// <inheritdoc />
    public Task<OrefGlucoseStatus> CalculateGlucoseStatusAsync(
        IEnumerable<OrefGlucoseReading> glucose
    )
    {
        if (_calculateGlucoseStatus == null || _memory == null)
        {
            _logger.LogWarning("Glucose status calculation not available - WASM module not loaded");
            return Task.FromResult(new OrefGlucoseStatus());
        }

        try
        {
            var inputJson = JsonSerializer.Serialize(glucose.ToArray(), JsonOptions);
            var resultPtr = CallWasmWithString(inputJson, _calculateGlucoseStatus);

            var resultJson = ReadWasmString(resultPtr);
            var result = JsonSerializer.Deserialize<OrefGlucoseStatus>(resultJson, JsonOptions);

            return Task.FromResult(result ?? new OrefGlucoseStatus());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Glucose status calculation failed");
            return Task.FromResult(new OrefGlucoseStatus());
        }
    }

    /// <inheritdoc />
    public Task<bool> HealthCheckAsync()
    {
        if (!_isAvailable)
        {
            _logger.LogWarning("oref WASM health check failed: module not loaded");
        }

        return Task.FromResult(_isAvailable);
    }

    #region WASM Memory Helpers

    /// <summary>
    /// Write a string to WASM memory and return pointer
    /// </summary>
    private int WriteWasmString(string value)
    {
        if (_memory == null || _store == null)
            throw new InvalidOperationException("WASM memory not available");

        var bytes = System.Text.Encoding.UTF8.GetBytes(value + '\0'); // null-terminated

        // Allocate memory if we have an alloc function, otherwise use a fixed offset
        int ptr;
        if (_alloc != null)
        {
            ptr = _alloc(bytes.Length);
        }
        else
        {
            // Use a simple bump allocator at high memory offset
            // This is a fallback when the module doesn't export alloc
            ptr = 1024; // Start at 1KB offset to avoid clobbering stack
        }

        // Get memory span at the target address
        var span = _memory.GetSpan(ptr, bytes.Length);
        bytes.CopyTo(span);

        return ptr;
    }

    /// <summary>
    /// Read a null-terminated string from WASM memory
    /// </summary>
    private string ReadWasmString(int ptr)
    {
        if (_memory == null || _store == null || ptr <= 0)
            return string.Empty;

        // Read in chunks to find null terminator
        const int ChunkSize = 4096;
        var maxRead = Math.Min((int)(_memory.GetSize() * 65536 - ptr), 1024 * 1024); // Limit to 1MB

        int length = 0;
        while (length < maxRead)
        {
            var readSize = Math.Min(ChunkSize, maxRead - length);
            var chunk = _memory.GetSpan(ptr + length, readSize);

            for (int i = 0; i < chunk.Length; i++)
            {
                if (chunk[i] == 0)
                {
                    // Found null terminator
                    return System.Text.Encoding.UTF8.GetString(_memory.GetSpan(ptr, length + i));
                }
            }
            length += readSize;
        }

        _logger.LogWarning("WASM string read exceeded limit without finding null terminator");
        return System.Text.Encoding.UTF8.GetString(_memory.GetSpan(ptr, maxRead));
    }

    /// <summary>
    /// Helper to call WASM function that takes a single string input
    /// </summary>
    private int CallWasmWithString(string input, Func<int, int> func)
    {
        var ptr = WriteWasmString(input);
        return func(ptr);
    }

    /// <summary>
    /// Helper to call WASM function that takes string and int
    /// </summary>
    private int CallWasmWithString(string input, Func<int, int, int> func)
    {
        var ptr = WriteWasmString(input);
        return func(ptr, input.Length);
    }

    #endregion

    #region IDisposable

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _store?.Dispose();
                _engine?.Dispose();
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    #endregion
}

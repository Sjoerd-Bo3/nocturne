namespace Nocturne.API.Services.Glucose;

/// <summary>
/// Configuration for <see cref="IPredictionService"/>. Bound from the
/// <c>Predictions</c> configuration section.
/// </summary>
public class PredictionOptions
{
    public const string SectionName = "Predictions";

    /// <summary>
    /// The source for glucose predictions.
    /// </summary>
    public PredictionSource Source { get; set; } = PredictionSource.None;
}

/// <summary>
/// Determines where <see cref="IPredictionService"/> reads glucose predictions from.
/// </summary>
public enum PredictionSource
{
    /// <summary>
    /// Predictions are disabled. The endpoint will return a 404.
    /// </summary>
    None,

    /// <summary>
    /// Read predictions from the most recent DeviceStatus (AAPS, Trio, Loop).
    /// The AID system calculates predictions on-device and uploads them.
    /// </summary>
    DeviceStatus,

    /// <summary>
    /// Run the oref algorithm server-side via the external WASM module.
    /// Requires oref.wasm from nightscout/nocturne-heuristics-wasm.
    /// Only useful for MDI or non-opensource AID systems.
    /// </summary>
    OrefWasm,
}

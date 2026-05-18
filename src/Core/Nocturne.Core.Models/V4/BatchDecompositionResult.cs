namespace Nocturne.Core.Models.V4;

/// <summary>
/// Aggregated result of decomposing a batch of legacy records.
/// Tracks per-record success/failure counts and collects individual results.
/// </summary>
/// <seealso cref="DecompositionResult"/>
public class BatchDecompositionResult
{
    /// <summary>
    /// Number of legacy records successfully decomposed into V4 records.
    /// </summary>
    public int Succeeded { get; set; }

    /// <summary>
    /// Number of legacy records that failed decomposition.
    /// </summary>
    public int Failed { get; set; }

    /// <summary>
    /// Per-record decomposition results, including created and updated V4 records.
    /// </summary>
    public List<DecompositionResult> Results { get; } = [];
}

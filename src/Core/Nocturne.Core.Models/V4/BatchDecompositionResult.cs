namespace Nocturne.Core.Models.V4;

/// <summary>
/// Aggregated result of decomposing a batch of legacy records.
/// Tracks per-record success/failure counts and collects individual results.
/// </summary>
public class BatchDecompositionResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<DecompositionResult> Results { get; } = [];
}

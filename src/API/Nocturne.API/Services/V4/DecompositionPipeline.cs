using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.V4;

/// <summary>
/// Unified orchestration layer that dispatches decomposition to the appropriate
/// <see cref="IDecomposer{T}"/> and absorbs errors internally (try-catch-log).
/// Parent services never need try-catch around decomposition calls.
/// </summary>
public class DecompositionPipeline : IDecompositionPipeline
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DecompositionPipeline> _logger;

    public DecompositionPipeline(IServiceProvider serviceProvider, ILogger<DecompositionPipeline> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<BatchDecompositionResult> DecomposeAsync<T>(IEnumerable<T> records, CancellationToken ct = default) where T : class
    {
        var result = new BatchDecompositionResult();
        var decomposer = ResolveDecomposer<T>();

        foreach (var record in records)
        {
            try
            {
                var decomposed = await decomposer.DecomposeAsync(record, ct);
                result.Succeeded++;
                result.Results.Add(decomposed);
            }
            catch (Exception ex)
            {
                result.Failed++;
                _logger.LogError(ex, "Failed to decompose {RecordType} into v4 tables", typeof(T).Name);
            }
        }

        return result;
    }

    public async Task<BatchDecompositionResult> DecomposeAsync<T>(T record, CancellationToken ct = default) where T : class
    {
        var result = new BatchDecompositionResult();
        var decomposer = ResolveDecomposer<T>();

        try
        {
            var decomposed = await decomposer.DecomposeAsync(record, ct);
            result.Succeeded++;
            result.Results.Add(decomposed);
        }
        catch (Exception ex)
        {
            result.Failed++;
            _logger.LogError(ex, "Failed to decompose {RecordType} into v4 tables", typeof(T).Name);
        }

        return result;
    }

    public async Task<int> DeleteByLegacyIdAsync<T>(string legacyId, CancellationToken ct = default) where T : class
    {
        var decomposer = ResolveDecomposer<T>();

        try
        {
            return await decomposer.DeleteByLegacyIdAsync(legacyId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete v4 records for legacy {RecordType} {LegacyId}", typeof(T).Name, legacyId);
            return 0;
        }
    }

    private IDecomposer<T> ResolveDecomposer<T>() where T : class
    {
        return _serviceProvider.GetRequiredService<IDecomposer<T>>();
    }
}

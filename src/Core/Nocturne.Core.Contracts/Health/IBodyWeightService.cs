using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Health;

/// <summary>
/// Domain service for <see cref="BodyWeight"/> record operations with WebSocket broadcasting.
/// </summary>
/// <seealso cref="BodyWeight"/>
public interface IBodyWeightService
{
    /// <summary>Returns body weight records with pagination.</summary>
    Task<IEnumerable<BodyWeight>> GetBodyWeightsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>Returns a single body weight record by ID, or null if not found.</summary>
    Task<BodyWeight?> GetBodyWeightByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>Creates one or more body weight records and returns the created entries.</summary>
    Task<IEnumerable<BodyWeight>> CreateBodyWeightsAsync(
        IEnumerable<BodyWeight> bodyWeights,
        CancellationToken cancellationToken = default
    );

    /// <summary>Updates an existing body weight record and returns the updated entry, or null if not found.</summary>
    Task<BodyWeight?> UpdateBodyWeightAsync(
        string id,
        BodyWeight bodyWeight,
        CancellationToken cancellationToken = default
    );

    /// <summary>Deletes a body weight record by ID and returns whether it existed.</summary>
    Task<bool> DeleteBodyWeightAsync(string id, CancellationToken cancellationToken = default);
}

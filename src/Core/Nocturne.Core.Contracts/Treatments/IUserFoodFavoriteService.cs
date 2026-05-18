using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Domain service for user food favorites.
/// </summary>
public interface IUserFoodFavoriteService
{
    /// <summary>
    /// Get the current user's favorite foods.
    /// </summary>
    Task<IEnumerable<Food>> GetFavoritesAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Add a food to the user's favorites.
    /// </summary>
    Task<bool> AddFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Remove a food from the user's favorites.
    /// </summary>
    Task<bool> RemoveFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Check if a food is a favorite for the user.
    /// </summary>
    Task<bool> IsFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get the user's recently used foods, excluding favorites.
    /// </summary>
    Task<IEnumerable<Food>> GetRecentFoodsAsync(
        string userId,
        int limit = 20,
        CancellationToken cancellationToken = default
    );
}

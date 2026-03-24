using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for user food favorites and recent foods.
/// </summary>
public class UserFoodFavoriteService : IUserFoodFavoriteService
{
    private readonly IUserFoodFavoriteRepository _favoriteRepository;
    private readonly ITreatmentFoodRepository _treatmentFoodRepository;
    private readonly ILogger<UserFoodFavoriteService> _logger;

    public UserFoodFavoriteService(
        IUserFoodFavoriteRepository favoriteRepository,
        ITreatmentFoodRepository treatmentFoodRepository,
        ILogger<UserFoodFavoriteService> logger
    )
    {
        _favoriteRepository = favoriteRepository;
        _treatmentFoodRepository = treatmentFoodRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFavoritesAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _favoriteRepository.GetFavoriteFoodsAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> AddFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    )
    {
        var created = await _favoriteRepository.AddFavoriteAsync(userId, foodId, cancellationToken);
        _logger.LogDebug(
            "Add favorite for user {UserId} and food {FoodId}: {Created}",
            userId,
            foodId,
            created != null
        );
        return created != null;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    )
    {
        var removed = await _favoriteRepository.RemoveFavoriteAsync(userId, foodId, cancellationToken);
        _logger.LogDebug(
            "Remove favorite for user {UserId} and food {FoodId}: {Removed}",
            userId,
            foodId,
            removed
        );
        return removed;
    }

    /// <inheritdoc />
    public async Task<bool> IsFavoriteAsync(
        string userId,
        Guid foodId,
        CancellationToken cancellationToken = default
    )
    {
        return await _favoriteRepository.IsFavoriteAsync(userId, foodId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetRecentFoodsAsync(
        string userId,
        int limit = 20,
        CancellationToken cancellationToken = default
    )
    {
        var favorites = await _favoriteRepository.GetFavoriteFoodsAsync(
            userId,
            cancellationToken
        );
        var favoriteIds = favorites.Select(f => f.Id).ToHashSet();

        var recentCandidates = await _treatmentFoodRepository.GetRecentFoodsAsync(
            limit + favoriteIds.Count,
            cancellationToken
        );

        var recents = recentCandidates
            .Where(food => !favoriteIds.Contains(food.Id))
            .Take(limit)
            .ToList();

        return recents;
    }
}

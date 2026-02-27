using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Constants;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for profile data operations with WebSocket broadcasting
/// </summary>
public class ProfileDataService : IProfileDataService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ISignalRBroadcastService _broadcastService;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ProfileDataService> _logger;
    private const string CollectionName = "profiles";
    private string TenantSlug => _tenantAccessor.Context?.Slug ?? "default";

    public ProfileDataService(
        IPostgreSqlService postgreSqlService,
        ISignalRBroadcastService broadcastService,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        ITenantAccessor tenantAccessor,
        ILogger<ProfileDataService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _broadcastService = broadcastService;
        _cacheService = cacheService;
        _cacheConfig = cacheConfig.Value;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> GetProfilesAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetProfilesAsync(count ?? 10, skip ?? 0, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetProfileByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        const string cacheKey = "profiles:current";
        var cacheTtl = TimeSpan.FromMinutes(10);

        var cachedProfile = await _cacheService.GetAsync<Profile>(cacheKey, cancellationToken);
        if (cachedProfile != null)
        {
            _logger.LogDebug("Cache HIT for current profile");
            return cachedProfile;
        }

        _logger.LogDebug("Cache MISS for current profile, fetching from database");
        // Get the current profile from MongoDB service
        var profile = await _postgreSqlService.GetCurrentProfileAsync(cancellationToken);

        if (profile != null)
        {
            await _cacheService.SetAsync(cacheKey, profile, cacheTtl, cancellationToken);
            _logger.LogDebug("Cached current profile with {TTL}min TTL", cacheTtl.TotalMinutes);
        }

        return profile;
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileAtTimestampAsync(
        long timestamp,
        CancellationToken cancellationToken = default
    )
    {
        var cacheKey = CacheKeyBuilder.BuildProfileAtTimestampKey(TenantSlug, timestamp);
        var cacheTtl = TimeSpan.FromSeconds(
            CacheConstants.Defaults.ProfileTimestampExpirationSeconds
        );

        return await _cacheService.GetOrSetAsync<Profile>(
            cacheKey,
            async () =>
            {
                _logger.LogDebug(
                    "Cache MISS for profile at timestamp {Timestamp}, fetching from database",
                    timestamp
                );

                var profile = await _postgreSqlService.GetProfileAtTimestampAsync(
                    timestamp,
                    cancellationToken
                );

                if (profile == null)
                {
                    _logger.LogDebug(
                        "No profile found at timestamp {Timestamp}, falling back to current profile",
                        timestamp
                    );
                    profile = await _postgreSqlService.GetCurrentProfileAsync(cancellationToken);
                }

                _logger.LogDebug(
                    "Retrieved profile for timestamp {Timestamp}: {ProfileId}",
                    timestamp,
                    profile?.Id ?? "null"
                );
                return profile ?? new Profile();
            },
            cacheTtl,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    )
    {
        var createdProfiles = await _postgreSqlService.CreateProfilesAsync(
            profiles,
            cancellationToken
        );

        // Invalidate current profile cache since new profiles were created
        try
        {
            await _cacheService.RemoveAsync("profiles:current", cancellationToken);
            _logger.LogDebug("Invalidated current profile cache after creating new profiles");

            // Invalidate all profile timestamp caches since profiles were created
            var profileTimestampPattern = CacheKeyBuilder.BuildProfileTimestampPattern(
                TenantSlug
            );
            await _cacheService.RemoveByPatternAsync(profileTimestampPattern, cancellationToken);
            _logger.LogDebug(
                "Invalidated profile timestamp pattern '{Pattern}' after creating {Count} profiles",
                profileTimestampPattern,
                createdProfiles.Count()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate profile caches");
        }

        // Broadcast create events for the profiles (replaces legacy ctx.bus.emit('storage-socket-create'))
        try
        {
            await _broadcastService.BroadcastStorageCreateAsync(
                CollectionName,
                new { colName = CollectionName, doc = createdProfiles }
            );
            _logger.LogDebug(
                "Broadcasted storage create event for {ProfileCount} profiles",
                createdProfiles.Count()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast storage create event for {ProfileCount} profiles",
                createdProfiles.Count()
            );
        }

        return createdProfiles;
    }

    /// <inheritdoc />
    public async Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    )
    {
        var updatedProfile = await _postgreSqlService.UpdateProfileAsync(
            id,
            profile,
            cancellationToken
        );

        if (updatedProfile != null)
        {
            // Invalidate current profile cache since a profile was updated
            try
            {
                await _cacheService.RemoveAsync("profiles:current", cancellationToken);
                _logger.LogDebug(
                    "Invalidated current profile cache after updating profile {ProfileId}",
                    id
                );

                // Invalidate all profile timestamp caches since a profile was updated
                var profileTimestampPattern = CacheKeyBuilder.BuildProfileTimestampPattern(
                    TenantSlug
                );
                await _cacheService.RemoveByPatternAsync(
                    profileTimestampPattern,
                    cancellationToken
                );
                _logger.LogDebug(
                    "Invalidated profile timestamp pattern '{Pattern}' after updating profile {ProfileId}",
                    profileTimestampPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate profile caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageUpdateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = updatedProfile }
                );
                _logger.LogDebug(
                    "Broadcasted storage update event for profile {ProfileId}",
                    updatedProfile.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage update event for profile {ProfileId}",
                    updatedProfile.Id
                );
            }
        }

        return updatedProfile;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Get the profile before deleting for broadcasting
        var profileToDelete = await _postgreSqlService.GetProfileByIdAsync(id, cancellationToken);

        var deleted = await _postgreSqlService.DeleteProfileAsync(id, cancellationToken);

        if (deleted)
        {
            // Invalidate current profile cache since a profile was deleted
            try
            {
                await _cacheService.RemoveAsync("profiles:current", cancellationToken);
                _logger.LogDebug(
                    "Invalidated current profile cache after deleting profile {ProfileId}",
                    id
                );

                // Invalidate all profile timestamp caches since a profile was deleted
                var profileTimestampPattern = CacheKeyBuilder.BuildProfileTimestampPattern(
                    TenantSlug
                );
                await _cacheService.RemoveByPatternAsync(
                    profileTimestampPattern,
                    cancellationToken
                );
                _logger.LogDebug(
                    "Invalidated profile timestamp pattern '{Pattern}' after deleting profile {ProfileId}",
                    profileTimestampPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate profile caches");
            }

            if (profileToDelete != null)
            {
                try
                {
                    await _broadcastService.BroadcastStorageDeleteAsync(
                        CollectionName,
                        new { colName = CollectionName, doc = profileToDelete }
                    );
                    _logger.LogDebug(
                        "Broadcasted storage delete event for profile {ProfileId}",
                        profileToDelete.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to broadcast storage delete event for profile {ProfileId}",
                        profileToDelete.Id
                    );
                }
            }
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteProfilesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: Implement BulkDeleteProfilesAsync in IDataService
        // For now, return 0 as bulk delete is not implemented for profiles
        _logger.LogWarning("Bulk delete for profiles is not implemented yet");
        return await Task.FromResult(0L);
    }
}

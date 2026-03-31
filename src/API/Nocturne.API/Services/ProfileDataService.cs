using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Constants;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for profile data operations with WebSocket broadcasting
/// </summary>
public class ProfileDataService : IProfileDataService
{
    private readonly IProfileRepository _profiles;
    private readonly IWriteSideEffects _sideEffects;
    private readonly IDataEventSink<Profile> _events;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<ProfileDataService> _logger;
    private const string CollectionName = "profiles";
    private string TenantCacheId => _tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public ProfileDataService(
        IProfileRepository profiles,
        IWriteSideEffects sideEffects,
        IDataEventSink<Profile> events,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        ITenantAccessor tenantAccessor,
        ILogger<ProfileDataService> logger
    )
    {
        _profiles = profiles;
        _sideEffects = sideEffects;
        _events = events;
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
        return await _profiles.GetProfilesAsync(count ?? 10, skip ?? 0, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _profiles.GetProfileByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        var cacheKey = CacheKeyBuilder.BuildCurrentProfileKey(TenantCacheId);
        var cacheTtl = TimeSpan.FromMinutes(10);

        var cachedProfile = await _cacheService.GetAsync<Profile>(cacheKey, cancellationToken);
        if (cachedProfile != null)
        {
            _logger.LogDebug("Cache HIT for current profile");
            return cachedProfile;
        }

        _logger.LogDebug("Cache MISS for current profile, fetching from database");
        // Get the current profile from MongoDB service
        var profile = await _profiles.GetCurrentProfileAsync(cancellationToken);

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
        var cacheKey = CacheKeyBuilder.BuildProfileAtTimestampKey(TenantCacheId, timestamp);
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

                var profile = await _profiles.GetProfileAtTimestampAsync(
                    timestamp,
                    cancellationToken
                );

                if (profile == null)
                {
                    _logger.LogDebug(
                        "No profile found at timestamp {Timestamp}, falling back to current profile",
                        timestamp
                    );
                    profile = await _profiles.GetCurrentProfileAsync(cancellationToken);
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
        var createdProfiles = await _profiles.CreateProfilesAsync(
            profiles,
            cancellationToken
        );

        await _sideEffects.OnCreatedAsync(
            CollectionName,
            createdProfiles.ToList(),
            cancellationToken: cancellationToken
        );

        await _events.OnCreatedAsync(createdProfiles.ToList(), cancellationToken);

        return createdProfiles;
    }

    /// <inheritdoc />
    public async Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    )
    {
        var updatedProfile = await _profiles.UpdateProfileAsync(
            id,
            profile,
            cancellationToken
        );

        if (updatedProfile != null)
        {
            await _sideEffects.OnUpdatedAsync(
                CollectionName,
                updatedProfile,
                cancellationToken: cancellationToken
            );

            await _events.OnUpdatedAsync(updatedProfile, cancellationToken);
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
        var profileToDelete = await _profiles.GetProfileByIdAsync(id, cancellationToken);

        var deleted = await _profiles.DeleteProfileAsync(id, cancellationToken);

        if (deleted)
        {
            await _sideEffects.OnDeletedAsync(
                CollectionName,
                profileToDelete,
                cancellationToken: cancellationToken
            );

            await _events.OnDeletedAsync(profileToDelete, cancellationToken);
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteProfilesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // TODO: Implement BulkDeleteProfilesAsync in IProfileRepository
        // For now, return 0 as bulk delete is not implemented for profiles
        _logger.LogWarning("Bulk delete for profiles is not implemented yet");
        return await Task.FromResult(0L);
    }
}

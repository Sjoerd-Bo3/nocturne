using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for Profile operations
/// </summary>
public class ProfileRepository : IProfileRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ProfileRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public ProfileRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get profiles with optional pagination
    /// </summary>
    /// <param name="count">The maximum number of profiles to return.</param>
    /// <param name="skip">The number of profiles to skip.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of profiles.</returns>
    public async Task<IEnumerable<Profile>> GetProfilesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        var entities = await _context
            .Profiles.OrderByDescending(p => p.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(ProfileMapper.ToDomainModel);
    }

    /// <summary>
    /// Get the most recent profile (current profile)
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current profile, or null if none exist.</returns>
    public async Task<Profile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        var entity = await _context
            .Profiles.OrderByDescending(p => p.Mills)
            .FirstOrDefaultAsync(cancellationToken);

        return entity != null ? ProfileMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get a profile by ID
    /// </summary>
    /// <param name="id">The unique identifier (GUID or legacy string ID).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile, or null if not found.</returns>
    public async Task<Profile?> GetProfileByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        ProfileEntity? entity = null;

        // Try to find by original MongoDB ID first
        if (!string.IsNullOrEmpty(id))
        {
            entity = await _context.Profiles.FirstOrDefaultAsync(
                p => p.OriginalId == id,
                cancellationToken
            );
        }

        // If not found and looks like a GUID, try by GUID
        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Profiles.FirstOrDefaultAsync(
                p => p.Id == guidId,
                cancellationToken
            );
        }

        return entity != null ? ProfileMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get profiles with advanced filtering support
    /// </summary>
    /// <param name="count">The maximum number of profiles to return.</param>
    /// <param name="skip">The number of profiles to skip.</param>
    /// <param name="findQuery">Optional search query string.</param>
    /// <param name="reverseResults">Whether to reverse the order of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of matching profiles.</returns>
    public async Task<IEnumerable<Profile>> GetProfilesWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Profiles.AsQueryable();

        // Apply basic filters based on query string
        if (!string.IsNullOrEmpty(findQuery))
        {
            // Simple text search in DefaultProfile and Units
            query = query.Where(p =>
                p.DefaultProfile.Contains(findQuery) || p.Units.Contains(findQuery)
            );
        }

        // Apply ordering
        query = reverseResults
            ? query.OrderBy(p => p.Mills)
            : query.OrderByDescending(p => p.Mills);

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(ProfileMapper.ToDomainModel);
    }

    /// <summary>
    /// Get the profile that was active at the specified timestamp
    /// </summary>
    /// <param name="timestamp">The unix milliseconds timestamp.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile active at that time, or a fallback if not found.</returns>
    public async Task<Profile?> GetProfileAtTimestampAsync(
        long timestamp,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await _context
            .Profiles.Where(p => p.Mills <= timestamp)
            .OrderByDescending(p => p.Mills)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity == null)
        {
            // Fallback to the earliest profile if the timestamp is before any recorded profile
            entity = await _context
                .Profiles.OrderBy(p => p.Mills)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return entity != null ? ProfileMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Create new profiles (uses upsert logic to handle duplicates)
    /// </summary>
    /// <param name="profiles">The collection of profiles to create.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of created or updated profiles.</returns>
    public async Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    )
    {
        var result = new List<ProfileEntity>();

        foreach (var profile in profiles)
        {
            var entity = ProfileMapper.ToEntity(profile);

            // Check if profile already exists by ID or OriginalId
            var existingEntity = await _context.Profiles.FirstOrDefaultAsync(
                p =>
                    p.Id == entity.Id
                    || (
                        !string.IsNullOrEmpty(entity.OriginalId)
                        && p.OriginalId == entity.OriginalId
                    ),
                cancellationToken
            );

            if (existingEntity != null)
            {
                // Update existing profile
                ProfileMapper.UpdateEntity(existingEntity, profile);
                result.Add(existingEntity);
            }
            else
            {
                // Add new profile
                await _context.Profiles.AddAsync(entity, cancellationToken);
                result.Add(entity);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return result.Select(ProfileMapper.ToDomainModel);
    }

    /// <summary>
    /// Update an existing profile
    /// </summary>
    /// <param name="id">The unique identifier of the profile to update.</param>
    /// <param name="profile">The updated profile data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated profile, or null if not found.</returns>
    public async Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await GetEntityByIdAsync(id, cancellationToken);
        if (entity == null)
            return null;

        ProfileMapper.UpdateEntity(entity, profile);
        await _context.SaveChangesAsync(cancellationToken);

        return ProfileMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete a profile by ID
    /// </summary>
    /// <param name="id">The unique identifier of the profile to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the profile was deleted, otherwise false.</returns>
    public async Task<bool> DeleteProfileAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        var entity = await GetEntityByIdAsync(id, cancellationToken);
        if (entity == null)
            return false;

        _context.Profiles.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Count profiles matching criteria
    /// </summary>
    /// <param name="findQuery">Optional search query string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total number of matching profiles.</returns>
    public async Task<long> CountProfilesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Profiles.AsQueryable();

        if (!string.IsNullOrEmpty(findQuery))
        {
            query = query.Where(p =>
                p.DefaultProfile.Contains(findQuery) || p.Units.Contains(findQuery)
            );
        }

        return await query.LongCountAsync(cancellationToken);
    }

    /// <summary>
    /// Helper method to get entity by ID (supports both GUID and MongoDB ObjectId)
    /// </summary>
    private async Task<ProfileEntity?> GetEntityByIdAsync(
        string id,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(id))
            return null;

        // Try original MongoDB ID first
        var entity = await _context.Profiles.FirstOrDefaultAsync(
            p => p.OriginalId == id,
            cancellationToken
        );

        // If not found and looks like a GUID, try by GUID
        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Profiles.FirstOrDefaultAsync(
                p => p.Id == guidId,
                cancellationToken
            );
        }

        return entity;
    }
}

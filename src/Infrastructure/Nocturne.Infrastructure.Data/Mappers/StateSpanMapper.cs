using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Mappers;

/// <summary>
/// Mapper for converting between StateSpan domain models and StateSpanEntity database entities
/// </summary>
public static class StateSpanMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static StateSpanEntity ToEntity(StateSpan stateSpan)
    {
        return new StateSpanEntity
        {
            Id = string.IsNullOrEmpty(stateSpan.Id)
                ? Guid.CreateVersion7()
                : ParseIdToGuid(stateSpan.Id),
            Category = stateSpan.Category.ToString(),
            State = stateSpan.State ?? string.Empty,
            StartTimestamp = stateSpan.StartTimestamp,
            EndTimestamp = stateSpan.EndTimestamp,
            Source = stateSpan.Source,
            MetadataJson = stateSpan.Metadata != null
                ? JsonSerializer.Serialize(stateSpan.Metadata)
                : null,
            OriginalId = stateSpan.OriginalId,
            SupersededById = !string.IsNullOrEmpty(stateSpan.SupersededById)
                ? ParseIdToGuid(stateSpan.SupersededById)
                : null,
            CreatedAt = stateSpan.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static StateSpan ToDomainModel(StateSpanEntity entity)
    {
        return new StateSpan
        {
            Id = entity.OriginalId ?? entity.Id.ToString(),
            Category = Enum.TryParse<StateSpanCategory>(entity.Category, out var category)
                ? category
                : StateSpanCategory.PumpMode,
            State = entity.State,
            StartTimestamp = entity.StartTimestamp,
            EndTimestamp = entity.EndTimestamp,
            Source = entity.Source,
            Metadata = DeserializeJsonProperty<Dictionary<string, object>>(entity.MetadataJson),
            OriginalId = entity.OriginalId,
            SupersededById = entity.SupersededById?.ToString(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(StateSpanEntity entity, StateSpan stateSpan)
    {
        entity.Category = stateSpan.Category.ToString();
        entity.State = stateSpan.State ?? string.Empty;
        entity.StartTimestamp = stateSpan.StartTimestamp;
        entity.EndTimestamp = stateSpan.EndTimestamp;
        entity.Source = stateSpan.Source;
        entity.MetadataJson = stateSpan.Metadata != null
            ? JsonSerializer.Serialize(stateSpan.Metadata)
            : null;
        entity.OriginalId = stateSpan.OriginalId;
        entity.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Parse string ID to GUID, or generate new GUID if invalid
    /// </summary>
    private static Guid ParseIdToGuid(string id)
    {
        if (string.IsNullOrEmpty(id))
            return Guid.CreateVersion7();

        if (Guid.TryParse(id, out var guidId))
            return guidId;

        // Hash the ID to get a deterministic GUID
        try
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hashBytes = sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
            var guidBytes = new byte[16];
            Array.Copy(hashBytes, guidBytes, 16);
            return new Guid(guidBytes);
        }
        catch
        {
            return Guid.CreateVersion7();
        }
    }

    /// <summary>
    /// Safely deserialize JSON property
    /// </summary>
    private static T? DeserializeJsonProperty<T>(string? json)
    {
        if (string.IsNullOrEmpty(json) || json == "null")
            return default;

        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch
        {
            return default;
        }
    }
}

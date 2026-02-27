using System.Text.Json;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between PumpDevice domain models and PumpDeviceEntity database entities
/// </summary>
public static class PumpDeviceMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static PumpDeviceEntity ToEntity(PumpDevice model)
    {
        return new PumpDeviceEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            PumpType = model.PumpType,
            PumpSerial = model.PumpSerial,
            FirstSeenTimestamp = model.FirstSeenTimestamp,
            LastSeenTimestamp = model.LastSeenTimestamp,
            AdditionalPropertiesJson = model.AdditionalProperties is { Count: > 0 }
                ? JsonSerializer.Serialize(model.AdditionalProperties)
                : null,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static PumpDevice ToDomainModel(PumpDeviceEntity entity)
    {
        return new PumpDevice
        {
            Id = entity.Id,
            PumpType = entity.PumpType,
            PumpSerial = entity.PumpSerial,
            FirstSeenTimestamp = entity.FirstSeenTimestamp,
            LastSeenTimestamp = entity.LastSeenTimestamp,
            AdditionalProperties = !string.IsNullOrEmpty(entity.AdditionalPropertiesJson)
                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(entity.AdditionalPropertiesJson)
                : null,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(PumpDeviceEntity entity, PumpDevice model)
    {
        entity.PumpType = model.PumpType;
        entity.PumpSerial = model.PumpSerial;
        entity.FirstSeenTimestamp = model.FirstSeenTimestamp;
        entity.LastSeenTimestamp = model.LastSeenTimestamp;
        entity.AdditionalPropertiesJson = model.AdditionalProperties is { Count: > 0 }
            ? JsonSerializer.Serialize(model.AdditionalProperties)
            : null;
    }
}

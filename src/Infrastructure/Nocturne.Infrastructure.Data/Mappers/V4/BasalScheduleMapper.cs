using System.Text.Json;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between BasalSchedule domain models and BasalScheduleEntity database entities
/// </summary>
public static class BasalScheduleMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static BasalScheduleEntity ToEntity(BasalSchedule model)
    {
        return new BasalScheduleEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            Mills = model.Mills,
            UtcOffset = model.UtcOffset,
            Device = model.Device,
            App = model.App,
            DataSource = model.DataSource,
            CorrelationId = model.CorrelationId,
            LegacyId = model.LegacyId,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
            ProfileName = model.ProfileName,
            EntriesJson = JsonSerializer.Serialize(model.Entries),
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static BasalSchedule ToDomainModel(BasalScheduleEntity entity)
    {
        return new BasalSchedule
        {
            Id = entity.Id,
            Mills = entity.Mills,
            UtcOffset = entity.UtcOffset,
            Device = entity.Device,
            App = entity.App,
            DataSource = entity.DataSource,
            CorrelationId = entity.CorrelationId,
            LegacyId = entity.LegacyId,
            CreatedAt = entity.SysCreatedAt,
            ModifiedAt = entity.SysUpdatedAt,
            ProfileName = entity.ProfileName,
            Entries = JsonSerializer.Deserialize<List<ScheduleEntry>>(entity.EntriesJson) ?? [],
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(BasalScheduleEntity entity, BasalSchedule model)
    {
        entity.Mills = model.Mills;
        entity.UtcOffset = model.UtcOffset;
        entity.Device = model.Device;
        entity.App = model.App;
        entity.DataSource = model.DataSource;
        entity.CorrelationId = model.CorrelationId;
        entity.LegacyId = model.LegacyId;
        entity.SysUpdatedAt = DateTime.UtcNow;
        entity.ProfileName = model.ProfileName;
        entity.EntriesJson = JsonSerializer.Serialize(model.Entries);
    }
}

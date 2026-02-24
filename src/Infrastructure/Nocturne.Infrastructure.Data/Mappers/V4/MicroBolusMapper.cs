using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between MicroBolus domain models and MicroBolusEntity database entities
/// </summary>
public static class MicroBolusMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static MicroBolusEntity ToEntity(MicroBolus model)
    {
        return new MicroBolusEntity
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
            Insulin = model.Insulin,
            SyncIdentifier = model.SyncIdentifier,
            PumpDeviceId = model.PumpDeviceId,
            PumpRecordId = model.PumpRecordId,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static MicroBolus ToDomainModel(MicroBolusEntity entity)
    {
        return new MicroBolus
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
            Insulin = entity.Insulin,
            SyncIdentifier = entity.SyncIdentifier,
            PumpDeviceId = entity.PumpDeviceId,
            PumpRecordId = entity.PumpRecordId,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(MicroBolusEntity entity, MicroBolus model)
    {
        entity.Mills = model.Mills;
        entity.UtcOffset = model.UtcOffset;
        entity.Device = model.Device;
        entity.App = model.App;
        entity.DataSource = model.DataSource;
        entity.CorrelationId = model.CorrelationId;
        entity.LegacyId = model.LegacyId;
        entity.SysUpdatedAt = DateTime.UtcNow;
        entity.Insulin = model.Insulin;
        entity.SyncIdentifier = model.SyncIdentifier;
        entity.PumpDeviceId = model.PumpDeviceId;
        entity.PumpRecordId = model.PumpRecordId;
    }
}

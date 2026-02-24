using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between TempBasal domain models and TempBasalEntity database entities
/// </summary>
public static class TempBasalMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static TempBasalEntity ToEntity(TempBasal model)
    {
        return new TempBasalEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            StartMills = model.StartMills,
            EndMills = model.EndMills,
            UtcOffset = model.UtcOffset,
            Device = model.Device,
            App = model.App,
            DataSource = model.DataSource,
            CorrelationId = model.CorrelationId,
            LegacyId = model.LegacyId,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
            Rate = model.Rate,
            ScheduledRate = model.ScheduledRate,
            Origin = model.Origin.ToString(),
            PumpDeviceId = model.PumpDeviceId,
            PumpRecordId = model.PumpRecordId,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static TempBasal ToDomainModel(TempBasalEntity entity)
    {
        return new TempBasal
        {
            Id = entity.Id,
            StartMills = entity.StartMills,
            EndMills = entity.EndMills,
            UtcOffset = entity.UtcOffset,
            Device = entity.Device,
            App = entity.App,
            DataSource = entity.DataSource,
            CorrelationId = entity.CorrelationId,
            LegacyId = entity.LegacyId,
            CreatedAt = entity.SysCreatedAt,
            ModifiedAt = entity.SysUpdatedAt,
            Rate = entity.Rate,
            ScheduledRate = entity.ScheduledRate,
            Origin = Enum.TryParse<TempBasalOrigin>(entity.Origin, out var origin)
                ? origin
                : TempBasalOrigin.Inferred,
            PumpDeviceId = entity.PumpDeviceId,
            PumpRecordId = entity.PumpRecordId,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(TempBasalEntity entity, TempBasal model)
    {
        entity.StartMills = model.StartMills;
        entity.EndMills = model.EndMills;
        entity.UtcOffset = model.UtcOffset;
        entity.Device = model.Device;
        entity.App = model.App;
        entity.DataSource = model.DataSource;
        entity.CorrelationId = model.CorrelationId;
        entity.LegacyId = model.LegacyId;
        entity.SysUpdatedAt = DateTime.UtcNow;
        entity.Rate = model.Rate;
        entity.ScheduledRate = model.ScheduledRate;
        entity.Origin = model.Origin.ToString();
        entity.PumpDeviceId = model.PumpDeviceId;
        entity.PumpRecordId = model.PumpRecordId;
    }
}

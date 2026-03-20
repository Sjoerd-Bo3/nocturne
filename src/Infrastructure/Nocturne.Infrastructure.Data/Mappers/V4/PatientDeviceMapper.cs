using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between PatientDevice domain models and PatientDeviceEntity database entities
/// </summary>
public static class PatientDeviceMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static PatientDeviceEntity ToEntity(PatientDevice model)
    {
        return new PatientDeviceEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            DeviceCategory = model.DeviceCategory.ToString(),
            Manufacturer = model.Manufacturer,
            Model = model.Model,
            AidAlgorithm = model.AidAlgorithm?.ToString(),
            SerialNumber = model.SerialNumber,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            IsCurrent = model.IsCurrent,
            Notes = model.Notes,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Convert database entity to domain model
    /// </summary>
    public static PatientDevice ToDomainModel(PatientDeviceEntity entity)
    {
        return new PatientDevice
        {
            Id = entity.Id,
            DeviceCategory = Enum.TryParse<DeviceCategory>(entity.DeviceCategory, ignoreCase: true, out var category)
                ? category
                : DeviceCategory.CGM,
            Manufacturer = entity.Manufacturer,
            Model = entity.Model,
            AidAlgorithm = entity.AidAlgorithm is not null
                && Enum.TryParse<AidAlgorithm>(entity.AidAlgorithm, ignoreCase: true, out var algorithm)
                    ? algorithm
                    : null,
            SerialNumber = entity.SerialNumber,
            StartDate = entity.StartDate,
            EndDate = entity.EndDate,
            IsCurrent = entity.IsCurrent,
            Notes = entity.Notes,
            CreatedAt = entity.SysCreatedAt,
            ModifiedAt = entity.SysUpdatedAt,
        };
    }

    /// <summary>
    /// Update existing entity with data from domain model
    /// </summary>
    public static void UpdateEntity(PatientDeviceEntity entity, PatientDevice model)
    {
        entity.DeviceCategory = model.DeviceCategory.ToString();
        entity.Manufacturer = model.Manufacturer;
        entity.Model = model.Model;
        entity.AidAlgorithm = model.AidAlgorithm?.ToString();
        entity.SerialNumber = model.SerialNumber;
        entity.StartDate = model.StartDate;
        entity.EndDate = model.EndDate;
        entity.IsCurrent = model.IsCurrent;
        entity.Notes = model.Notes;
        entity.SysUpdatedAt = DateTime.UtcNow;
    }
}

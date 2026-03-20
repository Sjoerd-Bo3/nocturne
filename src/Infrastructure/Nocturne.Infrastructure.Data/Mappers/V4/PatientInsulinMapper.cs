using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities.V4;

namespace Nocturne.Infrastructure.Data.Mappers.V4;

/// <summary>
/// Mapper for converting between PatientInsulin domain models and PatientInsulinEntity database entities
/// </summary>
public static class PatientInsulinMapper
{
    /// <summary>
    /// Convert domain model to database entity
    /// </summary>
    public static PatientInsulinEntity ToEntity(PatientInsulin model)
    {
        return new PatientInsulinEntity
        {
            Id = model.Id == Guid.Empty ? Guid.CreateVersion7() : model.Id,
            InsulinCategory = model.InsulinCategory.ToString(),
            Name = model.Name,
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
    public static PatientInsulin ToDomainModel(PatientInsulinEntity entity)
    {
        return new PatientInsulin
        {
            Id = entity.Id,
            InsulinCategory = Enum.TryParse<InsulinCategory>(entity.InsulinCategory, ignoreCase: true, out var category)
                ? category
                : InsulinCategory.RapidActing,
            Name = entity.Name,
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
    public static void UpdateEntity(PatientInsulinEntity entity, PatientInsulin model)
    {
        entity.InsulinCategory = model.InsulinCategory.ToString();
        entity.Name = model.Name;
        entity.StartDate = model.StartDate;
        entity.EndDate = model.EndDate;
        entity.IsCurrent = model.IsCurrent;
        entity.Notes = model.Notes;
        entity.SysUpdatedAt = DateTime.UtcNow;
    }
}

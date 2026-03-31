namespace Nocturne.Infrastructure.Data.Entities;

/// <summary>
/// Interface for entities that support soft deletion.
/// </summary>
public interface ISoftDeletable
{
    /// <summary>
    /// Gets or sets the date and time when the entity was soft deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }
}

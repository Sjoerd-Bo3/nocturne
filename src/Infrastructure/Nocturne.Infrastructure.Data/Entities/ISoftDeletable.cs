namespace Nocturne.Infrastructure.Data.Entities;

public interface ISoftDeletable
{
    DateTime? DeletedAt { get; set; }
}

namespace Nocturne.Core.Contracts.Storage;

/// <summary>
/// Abstraction for avatar image storage. Phase 1 uses PostgreSQL; swap to object storage later.
/// </summary>
public interface IAvatarStore
{
    Task<string> SaveAsync(Guid subjectId, Stream image, string contentType, CancellationToken ct = default);
    Task<AvatarData?> GetAsync(Guid subjectId, CancellationToken ct = default);
    Task DeleteAsync(Guid subjectId, CancellationToken ct = default);
}

public record AvatarData(Stream Data, string ContentType, int FileSize);

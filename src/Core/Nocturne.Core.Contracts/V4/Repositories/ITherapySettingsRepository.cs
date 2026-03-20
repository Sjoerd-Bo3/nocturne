using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ITherapySettingsRepository : IV4Repository<TherapySettings>
{
    Task<IEnumerable<TherapySettings>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<TherapySettings?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TherapySettings?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<IEnumerable<TherapySettings>> GetByProfileNameAsync(string profileName, CancellationToken ct = default);
    Task<TherapySettings> CreateAsync(TherapySettings model, CancellationToken ct = default);
    Task<TherapySettings> UpdateAsync(Guid id, TherapySettings model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<TherapySettings>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<TherapySettings>> BulkCreateAsync(
        IEnumerable<TherapySettings> records,
        CancellationToken ct = default
    );
}

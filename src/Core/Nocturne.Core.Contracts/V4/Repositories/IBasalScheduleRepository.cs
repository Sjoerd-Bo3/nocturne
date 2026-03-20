using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IBasalScheduleRepository : IV4Repository<BasalSchedule>
{
    Task<IEnumerable<BasalSchedule>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<BasalSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<BasalSchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<IEnumerable<BasalSchedule>> GetByProfileNameAsync(string profileName, CancellationToken ct = default);
    Task<BasalSchedule> CreateAsync(BasalSchedule model, CancellationToken ct = default);
    Task<BasalSchedule> UpdateAsync(Guid id, BasalSchedule model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<BasalSchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<BasalSchedule>> BulkCreateAsync(
        IEnumerable<BasalSchedule> records,
        CancellationToken ct = default
    );
}

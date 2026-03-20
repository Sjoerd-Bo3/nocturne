using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ITargetRangeScheduleRepository : IV4Repository<TargetRangeSchedule>
{
    Task<IEnumerable<TargetRangeSchedule>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<TargetRangeSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TargetRangeSchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<IEnumerable<TargetRangeSchedule>> GetByProfileNameAsync(string profileName, CancellationToken ct = default);
    Task<TargetRangeSchedule> CreateAsync(TargetRangeSchedule model, CancellationToken ct = default);
    Task<TargetRangeSchedule> UpdateAsync(Guid id, TargetRangeSchedule model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<TargetRangeSchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<TargetRangeSchedule>> BulkCreateAsync(
        IEnumerable<TargetRangeSchedule> records,
        CancellationToken ct = default
    );
}

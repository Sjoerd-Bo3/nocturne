using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ICarbRatioScheduleRepository
{
    Task<IEnumerable<CarbRatioSchedule>> GetAsync(
        long? from,
        long? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<CarbRatioSchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<CarbRatioSchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<IEnumerable<CarbRatioSchedule>> GetByProfileNameAsync(string profileName, CancellationToken ct = default);
    Task<CarbRatioSchedule> CreateAsync(CarbRatioSchedule model, CancellationToken ct = default);
    Task<CarbRatioSchedule> UpdateAsync(Guid id, CarbRatioSchedule model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default);
    Task<int> CountAsync(long? from, long? to, CancellationToken ct = default);
    Task<IEnumerable<CarbRatioSchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<CarbRatioSchedule>> BulkCreateAsync(
        IEnumerable<CarbRatioSchedule> records,
        CancellationToken ct = default
    );
}

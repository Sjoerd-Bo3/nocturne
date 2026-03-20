using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface ISensitivityScheduleRepository : IV4Repository<SensitivitySchedule>
{
    Task<IEnumerable<SensitivitySchedule>> GetAsync(
        DateTime? from,
        DateTime? to,
        string? device,
        string? source,
        int limit = 100,
        int offset = 0,
        bool descending = true,
        CancellationToken ct = default
    );
    Task<SensitivitySchedule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SensitivitySchedule?> GetByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<IEnumerable<SensitivitySchedule>> GetByProfileNameAsync(string profileName, CancellationToken ct = default);
    Task<SensitivitySchedule> CreateAsync(SensitivitySchedule model, CancellationToken ct = default);
    Task<SensitivitySchedule> UpdateAsync(Guid id, SensitivitySchedule model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdAsync(string legacyId, CancellationToken ct = default);
    Task<int> DeleteByLegacyIdPrefixAsync(string prefix, CancellationToken ct = default);
    Task<int> CountAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<IEnumerable<SensitivitySchedule>> GetByCorrelationIdAsync(
        Guid correlationId,
        CancellationToken ct = default
    );
    Task<IEnumerable<SensitivitySchedule>> BulkCreateAsync(
        IEnumerable<SensitivitySchedule> records,
        CancellationToken ct = default
    );
}

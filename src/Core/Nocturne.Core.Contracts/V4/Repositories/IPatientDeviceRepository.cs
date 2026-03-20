using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IPatientDeviceRepository
{
    Task<IEnumerable<PatientDevice>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<PatientDevice>> GetCurrentAsync(CancellationToken ct = default);
    Task<IEnumerable<PatientDevice>> GetByDateRangeAsync(DateTime from, DateTime to, CancellationToken ct = default);
    Task<PatientDevice?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PatientDevice> CreateAsync(PatientDevice model, CancellationToken ct = default);
    Task<PatientDevice> UpdateAsync(Guid id, PatientDevice model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}

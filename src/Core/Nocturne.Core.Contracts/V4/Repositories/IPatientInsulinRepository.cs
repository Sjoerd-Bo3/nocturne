using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IPatientInsulinRepository
{
    Task<IEnumerable<PatientInsulin>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<PatientInsulin>> GetCurrentAsync(CancellationToken ct = default);
    Task<PatientInsulin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<PatientInsulin> CreateAsync(PatientInsulin model, CancellationToken ct = default);
    Task<PatientInsulin> UpdateAsync(Guid id, PatientInsulin model, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<PatientInsulin?> GetPrimaryBolusInsulinAsync(CancellationToken ct = default);
    Task<PatientInsulin?> GetPrimaryBasalInsulinAsync(CancellationToken ct = default);
    Task SetPrimaryAsync(Guid insulinId, CancellationToken ct = default);
}

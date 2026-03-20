using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts.V4.Repositories;

public interface IPatientRecordRepository
{
    Task<PatientRecord?> GetAsync(CancellationToken ct = default);
    Task<PatientRecord> GetOrCreateAsync(CancellationToken ct = default);
    Task<PatientRecord> UpdateAsync(PatientRecord model, CancellationToken ct = default);
}

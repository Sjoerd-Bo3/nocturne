using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

public class PatientRecordRepository : IPatientRecordRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<PatientRecordRepository> _logger;

    public PatientRecordRepository(
        NocturneDbContext context,
        ILogger<PatientRecordRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PatientRecord?> GetAsync(CancellationToken ct = default)
    {
        var entity = await _context.PatientRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : PatientRecordMapper.ToDomainModel(entity);
    }

    public async Task<PatientRecord> GetOrCreateAsync(CancellationToken ct = default)
    {
        var entity = await _context.PatientRecords.FirstOrDefaultAsync(ct);

        if (entity is not null)
            return PatientRecordMapper.ToDomainModel(entity);

        _logger.LogInformation("No patient record found, creating empty record");

        var model = new PatientRecord
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
        };

        var newEntity = PatientRecordMapper.ToEntity(model);
        _context.PatientRecords.Add(newEntity);
        await _context.SaveChangesAsync(ct);

        return PatientRecordMapper.ToDomainModel(newEntity);
    }

    public async Task<PatientRecord> UpdateAsync(PatientRecord model, CancellationToken ct = default)
    {
        var entity = await _context.PatientRecords.FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Patient record not found");

        PatientRecordMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);

        return PatientRecordMapper.ToDomainModel(entity);
    }
}

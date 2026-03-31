using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Mappers.V4;

namespace Nocturne.Infrastructure.Data.Repositories.V4;

/// <summary>
/// Repository for managing patient insulin records (insulin types used) in the database.
/// </summary>
public class PatientInsulinRepository : IPatientInsulinRepository
{
    private readonly NocturneDbContext _context;
    private readonly ILogger<PatientInsulinRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PatientInsulinRepository"/> class.
    /// </summary>
    /// <param name="context">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    public PatientInsulinRepository(
        NocturneDbContext context,
        ILogger<PatientInsulinRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets all patient insulin records.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of patient insulins.</returns>
    public async Task<IEnumerable<PatientInsulin>> GetAllAsync(CancellationToken ct = default)
    {
        var entities = await _context.PatientInsulins
            .AsNoTracking()
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return entities.Select(PatientInsulinMapper.ToDomainModel);
    }

    /// <summary>
    /// Gets all currently used patient insulins.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A collection of current patient insulins.</returns>
    public async Task<IEnumerable<PatientInsulin>> GetCurrentAsync(CancellationToken ct = default)
    {
        var entities = await _context.PatientInsulins
            .AsNoTracking()
            .Where(e => e.IsCurrent)
            .OrderByDescending(e => e.StartDate)
            .ToListAsync(ct);

        return entities.Select(PatientInsulinMapper.ToDomainModel);
    }

    /// <summary>
    /// Gets a patient insulin record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The patient insulin record, or null if not found.</returns>
    public async Task<PatientInsulin?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.PatientInsulins.FindAsync([id], ct);
        return entity is null ? null : PatientInsulinMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Creates a new patient insulin record.
    /// </summary>
    /// <param name="model">The patient insulin to create.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The created patient insulin record.</returns>
    public async Task<PatientInsulin> CreateAsync(PatientInsulin model, CancellationToken ct = default)
    {
        var entity = PatientInsulinMapper.ToEntity(model);
        _context.PatientInsulins.Add(entity);
        await _context.SaveChangesAsync(ct);
        return PatientInsulinMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Updates an existing patient insulin record.
    /// </summary>
    /// <param name="id">The unique identifier of the record to update.</param>
    /// <param name="model">The updated record data.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The updated patient insulin record.</returns>
    public async Task<PatientInsulin> UpdateAsync(Guid id, PatientInsulin model, CancellationToken ct = default)
    {
        var entity = await _context.PatientInsulins.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"PatientInsulin {id} not found");

        PatientInsulinMapper.UpdateEntity(entity, model);
        await _context.SaveChangesAsync(ct);
        return PatientInsulinMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Deletes a patient insulin record by its unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _context.PatientInsulins.FindAsync([id], ct)
            ?? throw new KeyNotFoundException($"PatientInsulin {id} not found");

        _context.PatientInsulins.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Gets the primary bolus insulin currently in use.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The primary bolus insulin, or null if none found.</returns>
    public async Task<PatientInsulin?> GetPrimaryBolusInsulinAsync(CancellationToken ct = default)
    {
        var entity = await _context.PatientInsulins
            .AsNoTracking()
            .Where(e => e.IsCurrent && e.IsPrimary && (e.Role == "Bolus" || e.Role == "Both"))
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : PatientInsulinMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Gets the primary basal insulin currently in use.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The primary basal insulin, or null if none found.</returns>
    public async Task<PatientInsulin?> GetPrimaryBasalInsulinAsync(CancellationToken ct = default)
    {
        var entity = await _context.PatientInsulins
            .AsNoTracking()
            .Where(e => e.IsCurrent && e.IsPrimary && (e.Role == "Basal" || e.Role == "Both"))
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : PatientInsulinMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Sets a specific insulin record as primary, clearing other primary flags in the same role scope.
    /// </summary>
    /// <param name="insulinId">The unique identifier of the insulin record to set as primary.</param>
    /// <param name="ct">The cancellation token.</param>
    public async Task SetPrimaryAsync(Guid insulinId, CancellationToken ct = default)
    {
        var target = await _context.PatientInsulins.FindAsync([insulinId], ct)
            ?? throw new KeyNotFoundException($"PatientInsulin {insulinId} not found");

        // Determine which roles need to have their primary cleared
        var rolesToClear = target.Role switch
        {
            "Bolus" => new[] { "Bolus", "Both" },
            "Basal" => new[] { "Basal", "Both" },
            "Both" => new[] { "Bolus", "Basal", "Both" },
            _ => new[] { target.Role }
        };

        // Clear IsPrimary on all other insulins that share the same role scope
        var conflicting = await _context.PatientInsulins
            .Where(e => e.Id != insulinId && e.IsPrimary && rolesToClear.Contains(e.Role))
            .ToListAsync(ct);

        foreach (var entity in conflicting)
        {
            entity.IsPrimary = false;
            entity.SysUpdatedAt = DateTime.UtcNow;
        }

        target.IsPrimary = true;
        target.SysUpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }
}

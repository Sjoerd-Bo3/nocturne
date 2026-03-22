using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for alert tracker state and excursion persistence.
/// Methods are virtual to allow mocking with CallBase in tests.
/// </summary>
public class AlertTrackerRepository
{
    private readonly NocturneDbContext _context;

    public AlertTrackerRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get the tracker state for a specific alert rule.
    /// </summary>
    public virtual async Task<AlertTrackerStateEntity?> GetTrackerStateAsync(
        Guid alertRuleId,
        CancellationToken ct = default)
    {
        return await _context.AlertTrackerState
            .FirstOrDefaultAsync(s => s.AlertRuleId == alertRuleId, ct);
    }

    /// <summary>
    /// Insert or update the tracker state for a rule.
    /// </summary>
    public virtual async Task UpsertTrackerStateAsync(
        AlertTrackerStateEntity state,
        CancellationToken ct = default)
    {
        var existing = await _context.AlertTrackerState
            .FirstOrDefaultAsync(s => s.AlertRuleId == state.AlertRuleId, ct);

        if (existing == null)
        {
            _context.AlertTrackerState.Add(state);
        }
        else
        {
            existing.State = state.State;
            existing.ConfirmationCount = state.ConfirmationCount;
            existing.ActiveExcursionId = state.ActiveExcursionId;
            existing.UpdatedAt = state.UpdatedAt;
        }

        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Get the alert rule configuration.
    /// </summary>
    public virtual async Task<AlertRuleEntity?> GetRuleAsync(
        Guid alertRuleId,
        CancellationToken ct = default)
    {
        return await _context.AlertRules
            .FirstOrDefaultAsync(r => r.Id == alertRuleId, ct);
    }

    /// <summary>
    /// Create a new excursion record and return it.
    /// </summary>
    public virtual async Task<AlertExcursionEntity> CreateExcursionAsync(
        Guid alertRuleId,
        DateTime startedAt,
        CancellationToken ct = default)
    {
        var excursion = new AlertExcursionEntity
        {
            Id = Guid.CreateVersion7(),
            AlertRuleId = alertRuleId,
            StartedAt = startedAt,
        };

        _context.AlertExcursions.Add(excursion);
        await _context.SaveChangesAsync(ct);
        return excursion;
    }

    /// <summary>
    /// Close an excursion by setting its EndedAt timestamp.
    /// </summary>
    public virtual async Task CloseExcursionAsync(
        Guid excursionId,
        DateTime endedAt,
        CancellationToken ct = default)
    {
        var excursion = await _context.AlertExcursions
            .FirstOrDefaultAsync(e => e.Id == excursionId, ct);

        if (excursion != null)
        {
            excursion.EndedAt = endedAt;
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Record the start of hysteresis on an excursion.
    /// </summary>
    public virtual async Task SetHysteresisStartedAsync(
        Guid excursionId,
        DateTime hysteresisStartedAt,
        CancellationToken ct = default)
    {
        var excursion = await _context.AlertExcursions
            .FirstOrDefaultAsync(e => e.Id == excursionId, ct);

        if (excursion != null)
        {
            excursion.HysteresisStartedAt = hysteresisStartedAt;
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Clear the hysteresis timestamp on an excursion (when resuming from hysteresis).
    /// </summary>
    public virtual async Task ClearHysteresisAsync(
        Guid excursionId,
        CancellationToken ct = default)
    {
        var excursion = await _context.AlertExcursions
            .FirstOrDefaultAsync(e => e.Id == excursionId, ct);

        if (excursion != null)
        {
            excursion.HysteresisStartedAt = null;
            await _context.SaveChangesAsync(ct);
        }
    }
}

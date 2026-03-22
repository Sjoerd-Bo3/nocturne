using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// Controller for active alert state, history, and acknowledgement.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v4/alerts")]
[Tags("V4 Alerts")]
public class AlertsController : ControllerBase
{
    private readonly IDbContextFactory<NocturneDbContext> _contextFactory;
    private readonly IAlertAcknowledgementService _acknowledgementService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<AlertsController> _logger;

    public AlertsController(
        IDbContextFactory<NocturneDbContext> contextFactory,
        IAlertAcknowledgementService acknowledgementService,
        ITenantAccessor tenantAccessor,
        ILogger<AlertsController> logger)
    {
        _contextFactory = contextFactory;
        _acknowledgementService = acknowledgementService;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// List active (unresolved) excursions for the current tenant.
    /// </summary>
    [HttpGet("active")]
    [RemoteQuery]
    [ProducesResponseType(typeof(List<ActiveExcursionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ActiveExcursionResponse>>> GetActiveAlerts(CancellationToken ct)
    {
        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var excursions = await db.AlertExcursions
            .AsNoTracking()
            .Include(e => e.AlertRule)
            .Include(e => e.Instances)
            .Where(e => e.EndedAt == null)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync(ct);

        var result = excursions.Select(e => new ActiveExcursionResponse
        {
            Id = e.Id,
            AlertRuleId = e.AlertRuleId,
            RuleName = e.AlertRule?.Name ?? string.Empty,
            ConditionType = e.AlertRule?.ConditionType ?? string.Empty,
            StartedAt = e.StartedAt,
            AcknowledgedAt = e.AcknowledgedAt,
            AcknowledgedBy = e.AcknowledgedBy,
            HysteresisStartedAt = e.HysteresisStartedAt,
            ActiveInstances = e.Instances
                .Where(i => i.ResolvedAt == null)
                .Select(i => new ActiveInstanceResponse
                {
                    Id = i.Id,
                    ScheduleId = i.AlertScheduleId,
                    Status = i.Status,
                    CurrentStepOrder = i.CurrentStepOrder,
                    TriggeredAt = i.TriggeredAt,
                    NextEscalationAt = i.NextEscalationAt,
                })
                .ToList(),
        }).ToList();

        return Ok(result);
    }

    /// <summary>
    /// Get paginated history of resolved excursions.
    /// </summary>
    [HttpGet("history")]
    [RemoteQuery]
    [ProducesResponseType(typeof(AlertHistoryResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AlertHistoryResponse>> GetAlertHistory(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 1;
        if (pageSize > 100) pageSize = 100;

        await using var db = await _contextFactory.CreateDbContextAsync(ct);

        var query = db.AlertExcursions
            .AsNoTracking()
            .Include(e => e.AlertRule)
            .Where(e => e.EndedAt != null)
            .OrderByDescending(e => e.EndedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var result = new AlertHistoryResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            Items = items.Select(e => new HistoryExcursionResponse
            {
                Id = e.Id,
                AlertRuleId = e.AlertRuleId,
                RuleName = e.AlertRule?.Name ?? string.Empty,
                ConditionType = e.AlertRule?.ConditionType ?? string.Empty,
                StartedAt = e.StartedAt,
                EndedAt = e.EndedAt!.Value,
                AcknowledgedAt = e.AcknowledgedAt,
                AcknowledgedBy = e.AcknowledgedBy,
            }).ToList(),
        };

        return Ok(result);
    }

    /// <summary>
    /// Acknowledge all active alerts for the current tenant.
    /// </summary>
    [HttpPost("acknowledge")]
    [RemoteCommand(Invalidates = ["GetActiveAlerts"])]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> Acknowledge(
        [FromBody] AcknowledgeRequest request, CancellationToken ct)
    {
        var tenantId = _tenantAccessor.TenantId;

        await _acknowledgementService.AcknowledgeAllAsync(
            tenantId,
            request.AcknowledgedBy ?? "unknown",
            ct);

        return NoContent();
    }
}

#region DTOs

public class ActiveExcursionResponse
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
    public DateTime? HysteresisStartedAt { get; set; }
    public List<ActiveInstanceResponse> ActiveInstances { get; set; } = [];
}

public class ActiveInstanceResponse
{
    public Guid Id { get; set; }
    public Guid ScheduleId { get; set; }
    public string Status { get; set; } = string.Empty;
    public int CurrentStepOrder { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? NextEscalationAt { get; set; }
}

public class AlertHistoryResponse
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public List<HistoryExcursionResponse> Items { get; set; } = [];
}

public class HistoryExcursionResponse
{
    public Guid Id { get; set; }
    public Guid AlertRuleId { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public string ConditionType { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgedBy { get; set; }
}

public class AcknowledgeRequest
{
    public string? AcknowledgedBy { get; set; }
}

#endregion

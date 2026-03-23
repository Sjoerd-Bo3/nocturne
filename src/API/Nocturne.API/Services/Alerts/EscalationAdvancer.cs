using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Alerts;

public class EscalationAdvancer : IEscalationAdvancer
{
    private readonly IAlertRepository _repository;
    private readonly IAlertDeliveryService _deliveryService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EscalationAdvancer> _logger;

    public EscalationAdvancer(
        IAlertRepository repository,
        IAlertDeliveryService deliveryService,
        TimeProvider timeProvider,
        ILogger<EscalationAdvancer> logger)
    {
        _repository = repository;
        _deliveryService = deliveryService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task AdvanceAsync(AlertInstanceSnapshot instance, CancellationToken ct)
    {
        var steps = await _repository.GetEscalationStepsAsync(instance.AlertScheduleId, ct);
        var nextStepOrder = instance.CurrentStepOrder + 1;
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var nextStep = steps.FirstOrDefault(s => s.StepOrder == nextStepOrder);
        if (nextStep is null)
        {
            // No more steps; stop escalating
            await _repository.UpdateInstanceAsync(new UpdateAlertInstanceRequest(instance.Id,
                Status: "triggered", NextEscalationAt: DateTime.MinValue), ct);
            return;
        }

        var followingStep = steps.FirstOrDefault(s => s.StepOrder == nextStepOrder + 1);
        await _repository.UpdateInstanceAsync(new UpdateAlertInstanceRequest(instance.Id,
            CurrentStepOrder: nextStepOrder,
            Status: followingStep is null ? "triggered" : instance.Status,
            NextEscalationAt: followingStep is not null ? now.AddSeconds(nextStep.DelaySeconds) : DateTime.MinValue), ct);

        // Build payload and dispatch delivery
        var tenant = await _repository.GetTenantAlertContextAsync(instance.TenantId, ct);
        var activeCount = await _repository.CountActiveExcursionsAsync(instance.TenantId, ct);

        var payload = new AlertPayload
        {
            AlertType = "escalation",
            RuleName = "Escalated Alert",
            GlucoseValue = null,
            Trend = null,
            TrendRate = null,
            ReadingTimestamp = now,
            ExcursionId = instance.AlertExcursionId,
            InstanceId = instance.Id,
            TenantId = instance.TenantId,
            SubjectName = tenant?.SubjectName ?? "Unknown",
            ActiveExcursionCount = activeCount,
        };

        await _deliveryService.DispatchAsync(instance.Id, nextStepOrder, payload, ct);
        _logger.LogInformation("Escalated instance {InstanceId} to step {StepOrder}", instance.Id, nextStepOrder);
    }
}

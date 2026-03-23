using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertRepository
{
    Task<IReadOnlyList<AlertRuleSnapshot>> GetEnabledRulesAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<AlertScheduleSnapshot>> GetSchedulesForRuleAsync(Guid ruleId, CancellationToken ct);
    Task<IReadOnlyList<AlertEscalationStepSnapshot>> GetEscalationStepsAsync(Guid scheduleId, CancellationToken ct);
    Task<AlertInstanceSnapshot> CreateInstanceAsync(CreateAlertInstanceRequest request, CancellationToken ct);
    Task<IReadOnlyList<AlertInstanceSnapshot>> GetEscalatingInstancesDueAsync(DateTime asOf, CancellationToken ct);
    Task<IReadOnlyList<AlertInstanceSnapshot>> GetInstancesForExcursionAsync(Guid excursionId, CancellationToken ct);
    Task ResolveInstancesForExcursionAsync(Guid excursionId, DateTime resolvedAt, CancellationToken ct);
    Task UpdateInstanceAsync(UpdateAlertInstanceRequest request, CancellationToken ct);
    Task ExpirePendingDeliveriesAsync(IReadOnlyList<Guid> instanceIds, CancellationToken ct);
    Task<int> CountActiveExcursionsAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<HysteresisExcursionSnapshot>> GetExcursionsInHysteresisAsync(CancellationToken ct);
    Task CloseHysteresisExcursionAsync(Guid excursionId, Guid alertRuleId, DateTime endedAt, CancellationToken ct);
    Task<TenantAlertContext?> GetTenantAlertContextAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<SignalLossRuleSnapshot>> GetEnabledSignalLossRulesAsync(CancellationToken ct);
    Task<double?> GetLatestTrendRateAsync(Guid tenantId, CancellationToken ct);
    Task<IReadOnlyList<SnoozedInstanceSnapshot>> GetExpiredSnoozedInstancesAsync(DateTime asOf, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

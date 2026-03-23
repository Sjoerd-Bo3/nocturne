namespace Nocturne.Core.Models;

public record AlertRuleSnapshot(Guid Id, Guid TenantId, string Name, string ConditionType,
    string ConditionParams, int HysteresisMinutes, int ConfirmationReadings,
    string Severity, string ClientConfiguration, int SortOrder);

public record AlertScheduleSnapshot(Guid Id, Guid AlertRuleId, string Name, bool IsDefault,
    string? DaysOfWeek, TimeOnly? StartTime, TimeOnly? EndTime, string Timezone);

public record AlertEscalationStepSnapshot(Guid Id, Guid AlertScheduleId, int StepOrder, int DelaySeconds);

public record AlertInstanceSnapshot(Guid Id, Guid TenantId, Guid AlertExcursionId, Guid AlertScheduleId,
    int CurrentStepOrder, string Status, DateTime TriggeredAt,
    DateTime? NextEscalationAt, DateTime? SnoozedUntil, int SnoozeCount);

public record CreateAlertInstanceRequest(Guid TenantId, Guid ExcursionId, Guid ScheduleId,
    int InitialStepOrder, string Status, DateTime TriggeredAt, DateTime? NextEscalationAt);

public record UpdateAlertInstanceRequest(Guid Id, int? CurrentStepOrder = null, string? Status = null,
    DateTime? NextEscalationAt = null, DateTime? SnoozedUntil = null, int? SnoozeCount = null);

public record HysteresisExcursionSnapshot(Guid Id, Guid AlertRuleId, DateTime? HysteresisStartedAt, int HysteresisMinutes);

public record TenantAlertContext(Guid TenantId, string SubjectName, string? Slug, string? DisplayName,
    bool IsActive, DateTime? LastReadingAt);

public record SignalLossRuleSnapshot(Guid Id, Guid TenantId, string ConditionParams);

public record SnoozedInstanceSnapshot(Guid InstanceId, Guid TenantId, Guid AlertExcursionId,
    Guid AlertScheduleId, int CurrentStepOrder, string Status, int SnoozeCount,
    Guid AlertRuleId, string ConditionType, string ConditionParams, string ClientConfiguration);

using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

/// <summary>
/// Interface for the alert rules engine that evaluates glucose data against user-defined thresholds
/// </summary>
public interface IAlertRulesEngine
{
    /// <summary>
    /// Evaluates a V4 SensorGlucose reading against all active rules for a user and generates alert events
    /// </summary>
    Task<List<AlertEvent>> EvaluateGlucoseData(
        SensorGlucose glucoseReading,
        string userId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets all active alert rules for a specific user
    /// </summary>
    Task<AlertRuleEntity[]> GetActiveRulesForUser(
        string userId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks if an alert condition is met for a glucose reading against a specific rule
    /// </summary>
    Task<bool> IsAlertConditionMet(
        SensorGlucose glucoseReading,
        AlertRuleEntity rule,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks if a user is currently in their quiet hours
    /// </summary>
    Task<bool> IsUserInQuietHours(
        string userId,
        DateTime? checkTime = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates time-based conditions for an alert rule
    /// </summary>
    bool EvaluateTimeBasedConditions(AlertRuleEntity rule, DateTime checkTime);
}

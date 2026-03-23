using System.Text.Json;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Alerts;

/// <summary>
/// Resolves which schedule is active for a given alert rule at a given UTC time.
/// Schedules are evaluated in the tenant's timezone. The first matching non-default
/// schedule wins; if none match, the default schedule is returned.
/// </summary>
internal static class ScheduleResolver
{
    /// <summary>
    /// Resolve the active schedule for the given UTC time.
    /// </summary>
    /// <param name="schedules">All schedules for a single alert rule (must include exactly one default).</param>
    /// <param name="utcNow">Current time in UTC.</param>
    /// <returns>The matching non-default schedule, or the default schedule.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no default schedule exists.</exception>
    public static AlertScheduleEntity Resolve(
        IReadOnlyList<AlertScheduleEntity> schedules,
        DateTime utcNow)
    {
        var defaultSchedule = schedules.FirstOrDefault(s => s.IsDefault)
            ?? throw new InvalidOperationException("No default schedule found for alert rule.");

        foreach (var schedule in schedules.Where(s => !s.IsDefault))
        {
            if (IsActive(schedule.DaysOfWeek, schedule.StartTime, schedule.EndTime, schedule.Timezone, utcNow))
                return schedule;
        }

        return defaultSchedule;
    }

    /// <summary>
    /// Resolve the active schedule snapshot for the given UTC time.
    /// </summary>
    public static AlertScheduleSnapshot Resolve(
        IReadOnlyList<AlertScheduleSnapshot> schedules,
        DateTime utcNow)
    {
        var defaultSchedule = schedules.FirstOrDefault(s => s.IsDefault)
            ?? throw new InvalidOperationException("No default schedule found for alert rule.");

        foreach (var schedule in schedules.Where(s => !s.IsDefault))
        {
            if (IsActive(schedule.DaysOfWeek, schedule.StartTime, schedule.EndTime, schedule.Timezone, utcNow))
                return schedule;
        }

        return defaultSchedule;
    }

    private static bool IsActive(string? daysOfWeek, TimeOnly? startTime, TimeOnly? endTime, string timezone, DateTime utcNow)
    {
        if (startTime is null || endTime is null)
            return false;

        var tz = TimeZoneInfo.FindSystemTimeZoneById(timezone);
        var localDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, tz);
        var localTime = TimeOnly.FromDateTime(localDateTime);

        // Check day-of-week filter (ISO: 1=Mon..7=Sun)
        if (daysOfWeek is not null)
        {
            var allowedDays = JsonSerializer.Deserialize<int[]>(daysOfWeek);
            if (allowedDays is not null)
            {
                var isoDay = ToIsoDayOfWeek(localDateTime.DayOfWeek);
                if (!allowedDays.Contains(isoDay))
                    return false;
            }
        }

        // Check time window
        var start = startTime.Value;
        var end = endTime.Value;

        if (start <= end)
        {
            // Normal window (e.g., 09:00-17:00)
            return localTime >= start && localTime <= end;
        }

        // Cross-midnight window (e.g., 22:00-06:00)
        return localTime >= start || localTime <= end;
    }

    /// <summary>
    /// Convert .NET DayOfWeek (0=Sunday..6=Saturday) to ISO 8601 (1=Monday..7=Sunday).
    /// </summary>
    private static int ToIsoDayOfWeek(DayOfWeek day) =>
        day == DayOfWeek.Sunday ? 7 : (int)day;
}

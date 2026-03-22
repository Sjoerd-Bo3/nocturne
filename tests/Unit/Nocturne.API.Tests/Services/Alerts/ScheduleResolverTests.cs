using FluentAssertions;
using Nocturne.API.Services.Alerts;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Tests.Services.Alerts;

[Trait("Category", "Unit")]
public class ScheduleResolverTests
{
    private static AlertScheduleEntity MakeDefault(string name = "Default") => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsDefault = true,
        Timezone = "UTC"
    };

    private static AlertScheduleEntity MakeSchedule(
        string name,
        TimeOnly start,
        TimeOnly end,
        string timezone = "UTC",
        string? daysOfWeek = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        IsDefault = false,
        StartTime = start,
        EndTime = end,
        Timezone = timezone,
        DaysOfWeek = daysOfWeek
    };

    [Fact]
    public void Returns_default_when_no_non_default_schedules_exist()
    {
        var defaultSchedule = MakeDefault();
        var schedules = new List<AlertScheduleEntity> { defaultSchedule };

        var result = ScheduleResolver.Resolve(schedules, DateTime.UtcNow);

        result.Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Returns_default_when_non_default_schedules_do_not_match()
    {
        var defaultSchedule = MakeDefault();
        var business = MakeSchedule("Business", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, business };

        // 20:00 UTC is outside the 09:00-17:00 window
        var utcNow = new DateTime(2026, 3, 18, 20, 0, 0, DateTimeKind.Utc); // Wednesday

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Returns_matching_non_default_schedule_during_window()
    {
        var defaultSchedule = MakeDefault();
        var business = MakeSchedule("Business", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, business };

        // 12:00 UTC is inside the 09:00-17:00 window
        var utcNow = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc); // Wednesday

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(business);
    }

    [Fact]
    public void Day_of_week_filtering_excludes_non_matching_days()
    {
        var defaultSchedule = MakeDefault();
        // Mon-Fri schedule (ISO 1-5)
        var weekday = MakeSchedule("Weekday", new TimeOnly(9, 0), new TimeOnly(17, 0),
            daysOfWeek: "[1,2,3,4,5]");
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, weekday };

        // Saturday 12:00 UTC - day doesn't match
        var utcNow = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc); // Saturday

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Day_of_week_filtering_includes_matching_days()
    {
        var defaultSchedule = MakeDefault();
        // Mon-Fri schedule (ISO 1-5)
        var weekday = MakeSchedule("Weekday", new TimeOnly(9, 0), new TimeOnly(17, 0),
            daysOfWeek: "[1,2,3,4,5]");
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, weekday };

        // Wednesday 12:00 UTC - day matches (ISO 3 = Wednesday)
        var utcNow = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc); // Wednesday

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(weekday);
    }

    [Fact]
    public void Cross_midnight_window_matches_before_midnight()
    {
        var defaultSchedule = MakeDefault();
        var overnight = MakeSchedule("Overnight", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, overnight };

        // 23:00 UTC - after start, before midnight
        var utcNow = new DateTime(2026, 3, 18, 23, 0, 0, DateTimeKind.Utc);

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(overnight);
    }

    [Fact]
    public void Cross_midnight_window_matches_after_midnight()
    {
        var defaultSchedule = MakeDefault();
        var overnight = MakeSchedule("Overnight", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, overnight };

        // 02:00 UTC - after midnight, before end
        var utcNow = new DateTime(2026, 3, 19, 2, 0, 0, DateTimeKind.Utc);

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(overnight);
    }

    [Fact]
    public void Cross_midnight_window_does_not_match_midday()
    {
        var defaultSchedule = MakeDefault();
        var overnight = MakeSchedule("Overnight", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, overnight };

        // 12:00 UTC - outside the overnight window
        var utcNow = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Timezone_conversion_matches_correctly()
    {
        var defaultSchedule = MakeDefault();
        // 09:00-17:00 America/New_York (UTC-4 in March DST)
        var eastern = MakeSchedule("Eastern Business",
            new TimeOnly(9, 0), new TimeOnly(17, 0), "America/New_York");
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, eastern };

        // 14:00 UTC = 10:00 ET (inside window)
        var utcInside = new DateTime(2026, 3, 18, 14, 0, 0, DateTimeKind.Utc);
        ScheduleResolver.Resolve(schedules, utcInside).Should().BeSameAs(eastern);

        // 22:00 UTC = 18:00 ET (outside window)
        var utcOutside = new DateTime(2026, 3, 18, 22, 0, 0, DateTimeKind.Utc);
        ScheduleResolver.Resolve(schedules, utcOutside).Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Null_days_of_week_means_all_days_match()
    {
        var defaultSchedule = MakeDefault();
        var allDays = MakeSchedule("All Days", new TimeOnly(9, 0), new TimeOnly(17, 0),
            daysOfWeek: null);
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, allDays };

        // Sunday 12:00 UTC
        var utcNow = new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc); // Sunday

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(allDays);
    }

    [Fact]
    public void Sunday_uses_iso_day_7()
    {
        var defaultSchedule = MakeDefault();
        // Weekend only: Saturday=6, Sunday=7
        var weekend = MakeSchedule("Weekend", new TimeOnly(0, 0), new TimeOnly(23, 59),
            daysOfWeek: "[6,7]");
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, weekend };

        // Sunday 12:00 UTC
        var utcSunday = new DateTime(2026, 3, 22, 12, 0, 0, DateTimeKind.Utc);
        ScheduleResolver.Resolve(schedules, utcSunday).Should().BeSameAs(weekend);

        // Saturday 12:00 UTC
        var utcSaturday = new DateTime(2026, 3, 21, 12, 0, 0, DateTimeKind.Utc);
        ScheduleResolver.Resolve(schedules, utcSaturday).Should().BeSameAs(weekend);

        // Friday 12:00 UTC - should not match
        var utcFriday = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        ScheduleResolver.Resolve(schedules, utcFriday).Should().BeSameAs(defaultSchedule);
    }

    [Fact]
    public void Throws_when_no_default_schedule()
    {
        var nonDefault = MakeSchedule("Only", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var schedules = new List<AlertScheduleEntity> { nonDefault };

        var act = () => ScheduleResolver.Resolve(schedules, DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*default*");
    }

    [Fact]
    public void First_matching_schedule_wins()
    {
        var defaultSchedule = MakeDefault();
        var first = MakeSchedule("First", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var second = MakeSchedule("Second", new TimeOnly(10, 0), new TimeOnly(16, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, first, second };

        // 12:00 UTC - both match, first wins
        var utcNow = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

        var result = ScheduleResolver.Resolve(schedules, utcNow);

        result.Should().BeSameAs(first);
    }

    [Fact]
    public void Boundary_start_time_is_inclusive()
    {
        var defaultSchedule = MakeDefault();
        var schedule = MakeSchedule("Boundary", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, schedule };

        var utcNow = new DateTime(2026, 3, 18, 9, 0, 0, DateTimeKind.Utc);

        ScheduleResolver.Resolve(schedules, utcNow).Should().BeSameAs(schedule);
    }

    [Fact]
    public void Boundary_end_time_is_inclusive()
    {
        var defaultSchedule = MakeDefault();
        var schedule = MakeSchedule("Boundary", new TimeOnly(9, 0), new TimeOnly(17, 0));
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, schedule };

        var utcNow = new DateTime(2026, 3, 18, 17, 0, 0, DateTimeKind.Utc);

        ScheduleResolver.Resolve(schedules, utcNow).Should().BeSameAs(schedule);
    }

    [Fact]
    public void Schedule_without_start_end_times_does_not_match()
    {
        var defaultSchedule = MakeDefault();
        var incomplete = new AlertScheduleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Incomplete",
            IsDefault = false,
            StartTime = null,
            EndTime = null,
            Timezone = "UTC"
        };
        var schedules = new List<AlertScheduleEntity> { defaultSchedule, incomplete };

        var utcNow = new DateTime(2026, 3, 18, 12, 0, 0, DateTimeKind.Utc);

        ScheduleResolver.Resolve(schedules, utcNow).Should().BeSameAs(defaultSchedule);
    }
}

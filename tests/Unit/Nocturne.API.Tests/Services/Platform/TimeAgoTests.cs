using Xunit;
using Nocturne.API.Tests.Services.Glucose;

namespace Nocturne.API.Tests.Services.Platform;

/// <summary>
/// Tests for time-ago calculations with 1:1 legacy compatibility
/// Based on legacy timeago.test.js
/// </summary>
[Parity("timeago.test.js")]
public class TimeAgoTests
{
    private readonly TimeAgoService _timeAgoService = new();

    [Fact]
    public void CheckNotifications_ShouldNotTriggerWhenDataIsCurrent()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = new Entry { Mills = now, Mgdl = 100 };
        var settings = TimeAgoSettings.DefaultEnabled();

        var notifications = _timeAgoService.CheckNotifications(entry, settings, now);

        Assert.Empty(notifications);
    }

    [Fact]
    public void CheckNotifications_ShouldNotTriggerWhenDataIsInFuture()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = new Entry
        {
            Mills = now + (long)TimeSpan.FromMinutes(15).TotalMilliseconds,
            Mgdl = 100,
        };
        var settings = TimeAgoSettings.DefaultEnabled();

        var notifications = _timeAgoService.CheckNotifications(entry, settings, now);

        Assert.Empty(notifications);
    }

    [Fact]
    public void CheckNotifications_ShouldTriggerWarningWhenDataOlderThan15Minutes()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = new Entry
        {
            Mills = now - (long)TimeSpan.FromMinutes(16).TotalMilliseconds,
            Mgdl = 100,
        };
        var settings = TimeAgoSettings.DefaultEnabled();

        var notifications = _timeAgoService.CheckNotifications(entry, settings, now);

        Assert.Single(notifications);
        var notification = notifications[0];
        Assert.Equal(Levels.WARN, notification.Level);
        Assert.Equal("Last received: 16 mins ago\nBG Now: 100 mg/dl", notification.Message);
    }

    [Fact]
    public void CheckNotifications_ShouldTriggerUrgentWhenDataOlderThan30Minutes()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var entry = new Entry
        {
            Mills = now - (long)TimeSpan.FromMinutes(31).TotalMilliseconds,
            Mgdl = 100,
        };
        var settings = TimeAgoSettings.DefaultEnabled();

        var notifications = _timeAgoService.CheckNotifications(entry, settings, now);

        Assert.Single(notifications);
        var notification = notifications[0];
        Assert.Equal(Levels.URGENT, notification.Level);
        Assert.Equal("Last received: 31 mins ago\nBG Now: 100 mg/dl", notification.Message);
    }

    [Fact]
    public void CalcDisplay_ShouldMatchLegacyOutputs()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.Equal(
            new TimeAgoDisplay(null, "in the future", "future"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now + Minutes(15) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(1, "min ago", "m"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now + Minutes(4) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(null, "time ago", "ago"),
            _timeAgoService.CalcDisplay(null, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(1, "min ago", "m"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(1, "min ago", "m"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - 1 }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(1, "min ago", "m"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - Seconds(30) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(30, "mins ago", "m"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - Minutes(30) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(5, "hours ago", "h"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - Hours(5) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(5, "days ago", "d"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - Days(5) }, now)
        );

        Assert.Equal(
            new TimeAgoDisplay(null, "long ago", "ago"),
            _timeAgoService.CalcDisplay(new Entry { Mills = now - Days(10) }, now)
        );
    }

    private static long Minutes(int minutes) => (long)TimeSpan.FromMinutes(minutes).TotalMilliseconds;

    private static long Hours(int hours) => (long)TimeSpan.FromHours(hours).TotalMilliseconds;

    private static long Days(int days) => (long)TimeSpan.FromDays(days).TotalMilliseconds;

    private static long Seconds(int seconds) => (long)TimeSpan.FromSeconds(seconds).TotalMilliseconds;
}

public record TimeAgoDisplay(int? Value, string Label, string ShortLabel);

public sealed class TimeAgoSettings
{
    public bool EnableAlerts { get; set; } = true;
    public bool WarnEnabled { get; set; } = true;
    public bool UrgentEnabled { get; set; } = true;
    public int WarnMins { get; set; } = 15;
    public int UrgentMins { get; set; } = 30;
    public string Units { get; set; } = "mg/dl";

    public static TimeAgoSettings DefaultEnabled() => new();
}

public sealed class TimeAgoService
{
    private static readonly TimeSpan TwoMinutes = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);
    private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
    private static readonly TimeSpan TwoDays = TimeSpan.FromDays(2);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);
    private static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);

    public List<NotificationBase> CheckNotifications(
        Entry? entry,
        TimeAgoSettings settings,
        long now
    )
    {
        var notifications = new List<NotificationBase>();

        if (!settings.EnableAlerts)
        {
            return notifications;
        }

        if (entry == null || entry.Mills >= now)
        {
            return notifications;
        }

        var status = CheckStatus(entry, settings, now);
        if (status == TimeAgoStatus.Current)
        {
            return notifications;
        }

        var display = CalcDisplay(entry, now);
        var message = BuildMessage(entry, display, settings.Units);

        notifications.Add(
            new NotificationBase
            {
                Level = status == TimeAgoStatus.Urgent ? Levels.URGENT : Levels.WARN,
                Title = "Stale data, check rig?",
                Message = message,
                Group = "Time Ago",
                Timestamp = now,
            }
        );

        return notifications;
    }

    public TimeAgoDisplay CalcDisplay(Entry? entry, long now)
    {
        if (entry == null || entry.Mills == 0 || now == 0)
        {
            return new TimeAgoDisplay(null, "time ago", "ago");
        }

        if (entry.Mills - (long)FiveMinutes.TotalMilliseconds > now)
        {
            return new TimeAgoDisplay(null, "in the future", "future");
        }

        if (entry.Mills > now)
        {
            return new TimeAgoDisplay(1, "min ago", "m");
        }

        var timeSince = TimeSpan.FromMilliseconds(now - entry.Mills);

        if (timeSince < TwoMinutes)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromMinutes(1)),
                "min ago",
                "m"
            );
        }

        if (timeSince < OneHour)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromMinutes(1)),
                "mins ago",
                "m"
            );
        }

        if (timeSince < TwoHours)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromHours(1)),
                "hour ago",
                "h"
            );
        }

        if (timeSince < OneDay)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromHours(1)),
                "hours ago",
                "h"
            );
        }

        if (timeSince < TwoDays)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromDays(1)),
                "day ago",
                "d"
            );
        }

        if (timeSince < OneWeek)
        {
            return new TimeAgoDisplay(
                ClampRounded(timeSince.TotalMilliseconds, TimeSpan.FromDays(1)),
                "days ago",
                "d"
            );
        }

        return new TimeAgoDisplay(null, "long ago", "ago");
    }

    private static TimeAgoStatus CheckStatus(Entry entry, TimeAgoSettings settings, long now)
    {
        var timeSince = now - entry.Mills;

        if (
            settings.UrgentEnabled
            && timeSince > TimeSpan.FromMinutes(settings.UrgentMins).TotalMilliseconds
        )
        {
            return TimeAgoStatus.Urgent;
        }

        if (
            settings.WarnEnabled
            && timeSince > TimeSpan.FromMinutes(settings.WarnMins).TotalMilliseconds
        )
        {
            return TimeAgoStatus.Warn;
        }

        return TimeAgoStatus.Current;
    }

    private static string BuildMessage(Entry entry, TimeAgoDisplay display, string units)
    {
        var value = display.Value.HasValue ? display.Value.Value.ToString() : "";
        var header = $"Last received: {value} {display.Label}".Trim();
        var body = $"BG Now: {entry.Mgdl} {units}";
        return $"{header}\n{body}";
    }

    private static int ClampRounded(double totalMs, TimeSpan divisor)
    {
        var value = Math.Round(totalMs / divisor.TotalMilliseconds, MidpointRounding.AwayFromZero);
        return Math.Max(1, (int)value);
    }

    private enum TimeAgoStatus
    {
        Current,
        Warn,
        Urgent,
    }
}

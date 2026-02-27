using Nocturne.Connectors.MyLife.Mappers.Constants;
using Nocturne.Connectors.MyLife.Mappers.Helpers;
using Nocturne.Connectors.MyLife.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.MyLife.Mappers.Handlers;

/// <summary>
///     Handler for MyLife TempBasal events (event ID 4) - Temporary basal rate program.
///     These events represent user-initiated temporary basal programs (not algorithm-adjusted).
///     Produces TempBasal records.
/// </summary>
internal sealed class TempBasalHandler : IMyLifeStateSpanHandler
{
    public bool CanHandleStateSpan(MyLifeEvent ev)
    {
        return ev.EventTypeId == MyLifeEventType.TempBasal;
    }

    public IEnumerable<TempBasal> HandleStateSpan(MyLifeEvent ev, MyLifeContext context)
    {
        var info = MyLifeMapperHelpers.ParseInfo(ev.InformationFromDevice);

        // Try to get the rate - this event type can have either percentage or absolute rate
        double rate = 0;
        if (
            MyLifeMapperHelpers.TryGetInfoDouble(
                info,
                MyLifeJsonKeys.ValueInUperH,
                out var absoluteRate
            )
        )
            rate = absoluteRate;

        // TempBasal events (event ID 4) are user-initiated temporary basal programs.
        // This is different from IsTempBasalRate which indicates algorithm adjustments.
        // Origin is "Manual" for user-initiated temp basal programs.
        TempBasalOrigin origin;
        if (rate <= 0)
        {
            // Zero rate or percentage indicates suspended delivery
            // (though percentage-based would be relative to scheduled rate)
            if (
                MyLifeMapperHelpers.TryGetInfoDouble(
                    info,
                    MyLifeJsonKeys.Percentage,
                    out var percent
                )
                && percent <= 0
            )
                origin = TempBasalOrigin.Suspended;
            else if (
                rate <= 0
                && !MyLifeMapperHelpers.TryGetInfoDouble(info, MyLifeJsonKeys.Percentage, out _)
            )
                origin = TempBasalOrigin.Suspended;
            else
                origin = TempBasalOrigin.Manual;
        }
        else
        {
            // User-initiated temporary basal rate
            origin = TempBasalOrigin.Manual;
        }

        var tempBasal = MyLifeStateSpanFactory.CreateTempBasal(ev, rate, origin);

        // For user-initiated temp basals, we can calculate the end time
        // since the duration is explicit in the event
        if (
            MyLifeMapperHelpers.TryGetInfoDouble(
                info,
                MyLifeJsonKeys.Minutes,
                out var durationMinutes
            )
        )
        {
            tempBasal.EndTimestamp = tempBasal.StartTimestamp.AddMinutes(durationMinutes);
        }

        return [tempBasal];
    }
}

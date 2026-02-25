using Nocturne.Connectors.MyLife.Mappers.Constants;
using Nocturne.Connectors.MyLife.Mappers.Helpers;
using Nocturne.Connectors.MyLife.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.MyLife.Mappers.Handlers;

/// <summary>
///     Handler for MyLife BasalRate events (event ID 17) - Pump program basal rate changes.
///     These events report the current basal rate being delivered by the pump.
///     The IsTempBasalRate flag indicates if this is an algorithm-adjusted rate (CamAPS).
///     Produces TempBasal records.
/// </summary>
internal sealed class BasalRateHandler : IMyLifeStateSpanHandler
{
    public bool CanHandleStateSpan(MyLifeEvent ev)
    {
        return ev.EventTypeId == MyLifeEventType.BasalRate;
    }

    public IEnumerable<TempBasal> HandleStateSpan(MyLifeEvent ev, MyLifeContext context)
    {
        var info = MyLifeMapperHelpers.ParseInfo(ev.InformationFromDevice);
        if (!MyLifeMapperHelpers.TryGetInfoDouble(info, MyLifeJsonKeys.BasalRate, out var rate)) return [];

        var isTemp = MyLifeMapperHelpers.TryGetInfoBool(info, MyLifeJsonKeys.IsTempBasalRate);

        // Determine origin based on the event context:
        // - IsTempBasalRate = true means algorithm adjusted (CamAPS, Loop, etc.)
        // - IsTempBasalRate = false means scheduled basal from pump profile
        // - Rate = 0 indicates suspended delivery
        TempBasalOrigin origin;
        if (rate <= 0)
            origin = TempBasalOrigin.Suspended;
        else if (isTemp)
            // IsTempBasalRate = true indicates algorithm adjustment (e.g., CamAPS)
            origin = TempBasalOrigin.Algorithm;
        else
            // Regular basal rate from pump schedule
            origin = TempBasalOrigin.Scheduled;

        var tempBasal = MyLifeStateSpanFactory.CreateTempBasal(ev, rate, origin);
        return [tempBasal];
    }
}

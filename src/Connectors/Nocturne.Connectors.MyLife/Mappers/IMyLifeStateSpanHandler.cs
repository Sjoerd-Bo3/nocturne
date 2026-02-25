using Nocturne.Connectors.MyLife.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.MyLife.Mappers;

/// <summary>
///     Handler interface for converting MyLife basal events to TempBasal records
/// </summary>
internal interface IMyLifeStateSpanHandler
{
    /// <summary>
    ///     Determines if this handler can process the given event for TempBasal generation
    /// </summary>
    bool CanHandleStateSpan(MyLifeEvent ev);

    /// <summary>
    ///     Converts the event to TempBasal records
    /// </summary>
    IEnumerable<TempBasal> HandleStateSpan(MyLifeEvent ev, MyLifeContext context);
}

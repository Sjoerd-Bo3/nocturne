using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for calculating Insulin on Board (IOB) with 1:1 legacy JavaScript compatibility
/// Implements exact algorithms from ClientApp/lib/plugins/iob.js and ClientApp/src/lib/calculations/iob.ts
/// </summary>
public interface IIobService
{
    IobResult CalculateTotal(
        List<Treatment> treatments,
        List<DeviceStatus> deviceStatus,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null,
        List<TempBasal>? tempBasals = null
    );
    IobResult FromTreatments(
        List<Treatment> treatments,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null
    );
    IobResult FromTempBasals(
        List<TempBasal> tempBasals,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null
    );
    IobResult FromDeviceStatus(DeviceStatus deviceStatusEntry);
    IobResult LastIobDeviceStatus(List<DeviceStatus> deviceStatus, long time);
    IobContribution CalcTreatment(
        Treatment treatment,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null
    );
    IobContribution CalcBasalTreatment(
        Treatment treatment,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null
    );
    IobContribution CalcTempBasalIob(
        TempBasal tempBasal,
        IProfileService? profile = null,
        long? time = null,
        string? specProfile = null
    );
}

namespace Nocturne.Core.Models.V4;

/// <summary>
/// Indicates how a <see cref="TempBasal"/> was initiated or determined.
/// </summary>
/// <seealso cref="TempBasal"/>
/// <seealso cref="ApsSnapshot"/>
public enum TempBasalOrigin
{
    /// <summary>Temp basal was set automatically by an AID algorithm (e.g., OpenAPS, Loop, AAPS).</summary>
    Algorithm,

    /// <summary>Basal rate is running as-scheduled (no temp basal override active).</summary>
    Scheduled,

    /// <summary>Temp basal was set manually by the user.</summary>
    Manual,

    /// <summary>Pump delivery is suspended (zero basal).</summary>
    Suspended,

    /// <summary>Temp basal was inferred from pump history rather than directly reported.</summary>
    Inferred
}

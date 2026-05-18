using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Alerts;

/// <summary>
/// Operational status of an alert notification channel.
/// </summary>
/// <seealso cref="ChannelType"/>
/// <seealso cref="ChannelUnavailableReason"/>
[JsonConverter(typeof(JsonStringEnumConverter<ChannelStatus>))]
public enum ChannelStatus
{
    /// <summary>
    /// The channel is fully operational and can deliver notifications.
    /// </summary>
    [EnumMember(Value = "available")]
    Available,

    /// <summary>
    /// The channel is partially operational; delivery may be delayed or unreliable.
    /// </summary>
    [EnumMember(Value = "degraded")]
    Degraded,

    /// <summary>
    /// The channel cannot deliver notifications. See <see cref="ChannelUnavailableReason"/> for details.
    /// </summary>
    [EnumMember(Value = "unavailable")]
    Unavailable,
}

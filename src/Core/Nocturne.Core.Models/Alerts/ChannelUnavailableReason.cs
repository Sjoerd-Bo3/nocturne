using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Alerts;

/// <summary>
/// Reason why a notification <see cref="ChannelType"/> is currently <see cref="ChannelStatus.Unavailable"/>.
/// </summary>
/// <seealso cref="ChannelStatus"/>
/// <seealso cref="ChannelType"/>
[JsonConverter(typeof(JsonStringEnumConverter<ChannelUnavailableReason>))]
public enum ChannelUnavailableReason
{
    /// <summary>
    /// The adapter for this channel has not been configured (e.g., missing bot token or webhook URL).
    /// </summary>
    [EnumMember(Value = "adapter_not_configured")]
    AdapterNotConfigured,

    /// <summary>
    /// The adapter's last heartbeat is stale, indicating the service may be down or unresponsive.
    /// </summary>
    [EnumMember(Value = "heartbeat_stale")]
    HeartbeatStale,
}

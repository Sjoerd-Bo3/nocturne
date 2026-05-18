using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Nocturne.Core.Constants;

/// <summary>
/// WebSocket event types for Socket.IO communication between the Nocturne API
/// and connected clients (web frontend, mobile apps, bridge service).
/// </summary>
/// <remarks>
/// Serialized as lowercase/camelCase strings via <see cref="JsonStringEnumConverter"/>
/// and <see cref="EnumMemberAttribute"/> to match the Socket.IO wire protocol.
/// Events are broadcast through the SignalR hubs identified by
/// <see cref="ServiceNames.DataHub"/> and <see cref="ServiceNames.NotificationHub"/>.
/// Reconnection timing is governed by <see cref="ConnectorTimeouts.WebSocket"/>.
/// </remarks>
/// <seealso cref="ServiceNames.DataHub"/>
/// <seealso cref="ServiceNames.NotificationHub"/>
/// <seealso cref="ConnectorTimeouts.WebSocket"/>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WebSocketEvents
{
    // ========================================================================
    // Connection lifecycle events
    // ========================================================================

    /// <summary>
    /// Emitted when a client successfully establishes a WebSocket connection.
    /// </summary>
    [EnumMember(Value = "connect")]
    Connect,

    /// <summary>
    /// Emitted when a client disconnects (gracefully or due to transport failure).
    /// </summary>
    [EnumMember(Value = "disconnect")]
    Disconnect,

    /// <summary>
    /// Emitted when a connection attempt fails due to a transport or authentication error.
    /// </summary>
    [EnumMember(Value = "connect_error")]
    ConnectError,

    /// <summary>
    /// Emitted when a client successfully reconnects after a previous disconnection.
    /// </summary>
    [EnumMember(Value = "reconnect")]
    Reconnect,

    /// <summary>
    /// Emitted when all reconnection attempts have been exhausted without success.
    /// </summary>
    /// <seealso cref="ConnectorTimeouts.WebSocket.ReconnectAttempts"/>
    [EnumMember(Value = "reconnect_failed")]
    ReconnectFailed,

    /// <summary>
    /// Server acknowledgment sent after a successful connection handshake.
    /// </summary>
    [EnumMember(Value = "connect_ack")]
    ConnectAck,

    // ========================================================================
    // Data events
    // ========================================================================

    /// <summary>
    /// Broadcast when new CGM entries or device status data is available.
    /// Carries a payload containing the latest entries, device statuses, and computed properties.
    /// </summary>
    [EnumMember(Value = "dataUpdate")]
    DataUpdate,

    /// <summary>
    /// Broadcast when treatments (bolus, carbs, temp basal, etc.) are created or modified.
    /// </summary>
    [EnumMember(Value = "treatmentUpdate")]
    TreatmentUpdate,

    // ========================================================================
    // Storage (CRUD) events
    // ========================================================================

    /// <summary>
    /// Broadcast when a new record is created in any tenant-scoped collection.
    /// </summary>
    [EnumMember(Value = "create")]
    Create,

    /// <summary>
    /// Broadcast when an existing record is updated.
    /// </summary>
    [EnumMember(Value = "update")]
    Update,

    /// <summary>
    /// Broadcast when a record is deleted.
    /// </summary>
    [EnumMember(Value = "delete")]
    Delete,

    // ========================================================================
    // Notification and alarm events
    // ========================================================================

    /// <summary>
    /// Broadcast for user-facing announcements (e.g., site-wide messages).
    /// </summary>
    [EnumMember(Value = "announcement")]
    Announcement,

    /// <summary>
    /// Broadcast when a glucose alarm threshold is crossed (high or low).
    /// </summary>
    [EnumMember(Value = "alarm")]
    Alarm,

    /// <summary>
    /// Broadcast when an urgent glucose alarm threshold is crossed (urgent high or urgent low).
    /// </summary>
    [EnumMember(Value = "urgent_alarm")]
    UrgentAlarm,

    /// <summary>
    /// Broadcast to dismiss an active alarm after the glucose value returns to range.
    /// </summary>
    [EnumMember(Value = "clear_alarm")]
    ClearAlarm,

    /// <summary>
    /// General-purpose notification event for non-alarm messages (e.g., Pushover forwarding).
    /// </summary>
    [EnumMember(Value = "notification")]
    Notification,

    // ========================================================================
    // Status events
    // ========================================================================

    /// <summary>
    /// Broadcast when server status properties change (e.g., uploader connectivity).
    /// </summary>
    [EnumMember(Value = "statusUpdate")]
    StatusUpdate,

    /// <summary>
    /// Emitted in response to a client requesting the current server status snapshot.
    /// </summary>
    [EnumMember(Value = "status")]
    Status,

    // ========================================================================
    // Authentication events
    // ========================================================================

    /// <summary>
    /// Sent by the client to authenticate with the server using an API secret or token.
    /// </summary>
    [EnumMember(Value = "authenticate")]
    Authenticate,

    /// <summary>
    /// Sent by the server to confirm successful authentication and the granted permissions.
    /// </summary>
    [EnumMember(Value = "authenticated")]
    Authenticated,

    /// <summary>
    /// Sent by a client to subscribe to a specific data room/channel.
    /// </summary>
    [EnumMember(Value = "join")]
    Join,

    /// <summary>
    /// Sent by a client to unsubscribe from a data room/channel.
    /// </summary>
    [EnumMember(Value = "leave")]
    Leave,
}

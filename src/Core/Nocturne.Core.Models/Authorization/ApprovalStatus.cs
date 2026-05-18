using System.Text.Json.Serialization;

namespace Nocturne.Core.Models.Authorization;

/// <summary>
/// Approval status for subjects that request access.
/// </summary>
/// <seealso cref="Subject"/>
[JsonConverter(typeof(JsonStringEnumConverter<ApprovalStatus>))]
public enum ApprovalStatus
{
    /// <summary>
    /// Subject is approved and can authenticate (default for all existing subjects)
    /// </summary>
    Approved,

    /// <summary>
    /// Subject has requested access and is awaiting admin approval
    /// </summary>
    Pending
}

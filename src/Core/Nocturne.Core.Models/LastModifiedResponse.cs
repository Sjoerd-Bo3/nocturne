namespace Nocturne.Core.Models;

/// <summary>
/// Last modified timestamps response model for the <c>/api/v3/lastModified</c> endpoint.
/// Provides timestamps for when each collection was last modified, enabling efficient
/// incremental sync by clients that cache data locally.
/// </summary>
/// <seealso cref="V3StatusResponse"/>
public class LastModifiedResponse
{
    /// <summary>
    /// Last modified timestamp for entries collection
    /// </summary>
    public DateTime? Entries { get; set; }

    /// <summary>
    /// Last modified timestamp for treatments collection
    /// </summary>
    public DateTime? Treatments { get; set; }

    /// <summary>
    /// Last modified timestamp for profile collection
    /// </summary>
    public DateTime? Profile { get; set; }

    /// <summary>
    /// Last modified timestamp for devicestatus collection
    /// </summary>
    public DateTime? DeviceStatus { get; set; }

    /// <summary>
    /// Last modified timestamp for food collection
    /// </summary>
    public DateTime? Food { get; set; }

    /// <summary>
    /// Last modified timestamp for settings collection
    /// </summary>
    public DateTime? Settings { get; set; }

    /// <summary>
    /// Last modified timestamp for activity collection
    /// </summary>
    public DateTime? Activity { get; set; }

    /// <summary>
    /// Server time when the response was generated
    /// </summary>
    public DateTime ServerTime { get; set; }

    /// <summary>
    /// Additional collection timestamps
    /// For future extensibility and custom collections
    /// </summary>
    public Dictionary<string, DateTime> Additional { get; set; } = new();
}

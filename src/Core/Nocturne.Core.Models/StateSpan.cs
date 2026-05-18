using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents a time-ranged system state (pump mode, connectivity, override, profile).
/// </summary>
/// <remarks>
/// <para><see cref="StartMills"/> and <see cref="EndMills"/> are computed properties that convert
/// from <see cref="StartTimestamp"/> and <see cref="EndTimestamp"/> to Unix milliseconds for v1/v3 API compatibility.</para>
/// <para><see cref="IsActive"/> returns <c>true</c> when <see cref="EndTimestamp"/> is null,
/// indicating the state is still in effect.</para>
/// </remarks>
/// <seealso cref="StateSpanCategory"/>
/// <seealso cref="Entry"/>
/// <seealso cref="Treatment"/>
public class StateSpan
{
    /// <summary>
    /// Gets or sets the unique identifier (UUID or original source ID)
    /// </summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets the state category
    /// </summary>
    [JsonPropertyName("category")]
    public StateSpanCategory Category { get; set; }

    /// <summary>
    /// Gets or sets the state value within the category
    /// </summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>
    /// Gets or sets when this state began as UTC DateTime
    /// </summary>
    [JsonPropertyName("startTimestamp")]
    public DateTime StartTimestamp { get; set; }

    /// <summary>
    /// Gets or sets when this state ended as UTC DateTime (null = active)
    /// </summary>
    [JsonPropertyName("endTimestamp")]
    public DateTime? EndTimestamp { get; set; }

    /// <summary>
    /// When this state began in Unix milliseconds (computed for v1/v3 compatibility)
    /// </summary>
    [JsonPropertyName("startMills")]
    public long StartMills => new DateTimeOffset(StartTimestamp, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>
    /// When this state ended in Unix milliseconds (computed for v1/v3 compatibility)
    /// </summary>
    [JsonPropertyName("endMills")]
    public long? EndMills => EndTimestamp.HasValue ? new DateTimeOffset(EndTimestamp.Value, TimeSpan.Zero).ToUnixTimeMilliseconds() : null;

    /// <summary>
    /// Gets or sets the data source identifier
    /// </summary>
    [JsonPropertyName("source")]
    public string? Source { get; set; }

    /// <summary>
    /// Gets or sets category-specific metadata (stored as JSON)
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object>? Metadata { get; set; }

    /// <summary>
    /// Gets or sets the original ID from source system for deduplication
    /// </summary>
    [JsonPropertyName("originalId")]
    public string? OriginalId { get; set; }

    /// <summary>
    /// Gets or sets the created at timestamp
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the updated at timestamp
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Returns true if this state span is currently active (no end time)
    /// </summary>
    [JsonIgnore]
    public bool IsActive => !EndTimestamp.HasValue;

    /// <summary>
    /// Gets or sets the ID of the span that superseded this one.
    /// Set when a new exclusive span (Override, TemporaryTarget, Profile) closes this span.
    /// </summary>
    [JsonPropertyName("supersededById")]
    public string? SupersededById { get; set; }

    /// <summary>
    /// Gets or sets the canonical group ID for deduplication.
    /// Records with the same CanonicalId represent the same underlying event from different sources.
    /// </summary>
    [JsonPropertyName("canonicalId")]
    public Guid? CanonicalId { get; set; }

    /// <summary>
    /// Gets or sets the list of data sources that contributed to this unified record.
    /// Only populated when returning merged/unified DTOs.
    /// </summary>
    [JsonPropertyName("sources")]
    public string[]? Sources { get; set; }
}

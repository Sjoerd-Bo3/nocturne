using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Status of a compression low suggestion.
/// Tracks the lifecycle of a <see cref="CompressionLowSuggestion"/> from detection through user review.
/// </summary>
/// <seealso cref="CompressionLowSuggestion"/>
[JsonConverter(typeof(JsonStringEnumConverter<CompressionLowStatus>))]
public enum CompressionLowStatus
{
    /// <summary>
    /// Suggestion is pending user review
    /// </summary>
    Pending,

    /// <summary>
    /// User accepted the suggestion, StateSpan created
    /// </summary>
    Accepted,

    /// <summary>
    /// User dismissed the suggestion
    /// </summary>
    Dismissed
}

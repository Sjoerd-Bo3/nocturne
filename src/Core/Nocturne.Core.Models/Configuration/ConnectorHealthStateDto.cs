namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Health state information for a data connector, tracking sync success and failure history.
/// </summary>
/// <seealso cref="Nocturne.Core.Models.Services.ConnectorCapabilities"/>
public class ConnectorHealthStateDto
{
    /// <summary>When the most recent sync attempt occurred (successful or not).</summary>
    public DateTime? LastSyncAttempt { get; set; }

    /// <summary>When the most recent successful sync completed.</summary>
    public DateTime? LastSuccessfulSync { get; set; }

    /// <summary>Error message from the most recent failed sync, if any.</summary>
    public string? LastErrorMessage { get; set; }

    /// <summary>When the most recent error occurred.</summary>
    public DateTime? LastErrorAt { get; set; }

    /// <summary>Whether the connector is considered healthy based on recent sync history.</summary>
    public bool IsHealthy { get; set; }
}

namespace Nocturne.Core.Models.Configuration;

/// <summary>
/// Health state information for a connector
/// </summary>
public class ConnectorHealthStateDto
{
    public DateTime? LastSyncAttempt { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public bool IsHealthy { get; set; }
}

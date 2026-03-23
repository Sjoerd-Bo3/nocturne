namespace Nocturne.API.Models;

/// <summary>
/// Standard error response DTO for API endpoints
/// </summary>
public class ErrorResponse
{
    /// <summary>
    /// Machine-readable error code
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

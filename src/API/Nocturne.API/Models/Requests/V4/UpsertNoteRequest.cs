namespace Nocturne.API.Models.Requests.V4;

public class UpsertNoteRequest
{
    public DateTimeOffset Timestamp { get; set; }
    public int? UtcOffset { get; set; }
    public string? Device { get; set; }
    public string? App { get; set; }
    public string? DataSource { get; set; }
    public string? Text { get; set; }
    public string? EventType { get; set; }
    public bool IsAnnouncement { get; set; }
    public string? SyncIdentifier { get; set; }
}

namespace Nocturne.Core.Models.CoachMarks;

/// <summary>
/// Tracks a single user's progression through a coach mark (unseen, seen, dismissed, completed).
/// </summary>
public class CoachMarkState
{
    public Guid Id { get; set; }
    public Guid SubjectId { get; set; }
    public string MarkKey { get; set; } = string.Empty;
    public string Status { get; set; } = "unseen"; // unseen, seen, dismissed, completed
    public DateTime? SeenAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

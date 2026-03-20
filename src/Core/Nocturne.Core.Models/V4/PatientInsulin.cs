namespace Nocturne.Core.Models.V4;

public class PatientInsulin
{
    public Guid Id { get; set; }
    public InsulinCategory InsulinCategory { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

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
    public string? FormulationId { get; set; }
    public double Dia { get; set; } = 4.0;
    public int Peak { get; set; } = 75;
    public string Curve { get; set; } = "rapid-acting";
    public int Concentration { get; set; } = 100;
    public InsulinRole Role { get; set; } = InsulinRole.Both;
    public bool IsPrimary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

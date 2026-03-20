namespace Nocturne.Core.Models.V4;

public class PatientRecord
{
    public Guid Id { get; set; }
    public DiabetesType? DiabetesType { get; set; }
    public string? DiabetesTypeOther { get; set; }
    public DateOnly? DiagnosisDate { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? PreferredName { get; set; }
    public string? Pronouns { get; set; }
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

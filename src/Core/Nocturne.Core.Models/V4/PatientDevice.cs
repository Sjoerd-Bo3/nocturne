namespace Nocturne.Core.Models.V4;

public class PatientDevice
{
    public Guid Id { get; set; }
    public DeviceCategory DeviceCategory { get; set; }
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public AidAlgorithm? AidAlgorithm { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? DeviceId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
}

namespace Nocturne.Connectors.MyLife.Models;

public class MyLifePumpSettingsReadout
{
    public string? ETag { get; set; }
    public List<object>? DeviceSettings { get; set; }
    public long LastEditTimeStamp { get; set; }
    public object? YpsopumpSettings151 { get; set; }
    public string? PumpBaseServiceVersion { get; set; }
    public string? SettingsServiceVersion { get; set; }
    public string? HistoryServiceVersion { get; set; }
    public int TimeDeltaInSeconds { get; set; }
    public string? Version { get; set; }
    public object? YpsopumpSettings15 { get; set; }
    public string? Id { get; set; }
    public string? PatientId { get; set; }
    public List<MyLifeBasalProgram>? BasalPrograms { get; set; }
    public string? DeviceName { get; set; }
    public int DeviceTypeId { get; set; }
    public string? DeviceSerialNumber { get; set; }
    public string? ActiveBasalProgramName { get; set; }
    public long UploadDateTime { get; set; }
    public object? OmnipodSettings { get; set; }
    public object? YpsopumpSettings { get; set; }
}

namespace Nocturne.Connectors.Core.Interfaces;

public interface IConnectorPublisher
{
    bool IsAvailable { get; }
    IGlucosePublisher Glucose { get; }
    ITreatmentPublisher Treatments { get; }
    IDevicePublisher Device { get; }
    IMetadataPublisher Metadata { get; }
}

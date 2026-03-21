using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.API.Services.ConnectorPublishing;

public class InProcessConnectorPublisher : IConnectorPublisher
{
    public bool IsAvailable => true;
    public IGlucosePublisher Glucose { get; }
    public ITreatmentPublisher Treatments { get; }
    public IDevicePublisher Device { get; }
    public IMetadataPublisher Metadata { get; }

    public InProcessConnectorPublisher(
        IGlucosePublisher glucose,
        ITreatmentPublisher treatments,
        IDevicePublisher device,
        IMetadataPublisher metadata)
    {
        Glucose = glucose;
        Treatments = treatments;
        Device = device;
        Metadata = metadata;
    }
}

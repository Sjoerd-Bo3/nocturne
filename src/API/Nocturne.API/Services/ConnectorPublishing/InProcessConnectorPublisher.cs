using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.API.Services.ConnectorPublishing;

/// <summary>
/// In-process implementation of <see cref="IConnectorPublisher"/> that wires connector output
/// directly to the running API services without HTTP or message-queue overhead.
/// Used when connectors run in the same process as the API (the default Aspire configuration).
/// </summary>
/// <seealso cref="IConnectorPublisher"/>
/// <seealso cref="IGlucosePublisher"/>
/// <seealso cref="ITreatmentPublisher"/>
/// <seealso cref="IDevicePublisher"/>
/// <seealso cref="IMetadataPublisher"/>
public class InProcessConnectorPublisher : IConnectorPublisher
{
    /// <inheritdoc />
    public bool IsAvailable => true;

    /// <inheritdoc />
    public IGlucosePublisher Glucose { get; }

    /// <inheritdoc />
    public ITreatmentPublisher Treatments { get; }

    /// <inheritdoc />
    public IDevicePublisher Device { get; }

    /// <inheritdoc />
    public IMetadataPublisher Metadata { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="InProcessConnectorPublisher"/>.
    /// </summary>
    /// <param name="glucose">The glucose publisher for incoming CGM readings.</param>
    /// <param name="treatments">The treatment publisher for bolus, basal, and carb data.</param>
    /// <param name="device">The device publisher for device status and metadata.</param>
    /// <param name="metadata">The metadata publisher for connector-level metadata.</param>
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

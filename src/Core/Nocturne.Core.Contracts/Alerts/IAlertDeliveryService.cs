using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertDeliveryService
{
    Task DispatchAsync(Guid alertInstanceId, int stepOrder, AlertPayload payload, CancellationToken ct);
    Task MarkDeliveredAsync(Guid deliveryId, string? platformMessageId, string? platformThreadId, CancellationToken ct);
    Task MarkFailedAsync(Guid deliveryId, string error, CancellationToken ct);
}

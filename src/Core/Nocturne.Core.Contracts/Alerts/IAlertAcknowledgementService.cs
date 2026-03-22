namespace Nocturne.Core.Contracts.Alerts;

public interface IAlertAcknowledgementService
{
    Task AcknowledgeAllAsync(Guid tenantId, string acknowledgedBy, CancellationToken ct);
}

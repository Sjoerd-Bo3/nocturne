using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Alerts;

public interface IEscalationAdvancer
{
    Task AdvanceAsync(AlertInstanceSnapshot instance, CancellationToken ct);
}

using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts.Treatments;

/// <summary>
/// Driven port for treatment write-event propagation. The adapter translates
/// these into SignalR broadcasts. Failures are non-fatal.
/// </summary>
public interface ITreatmentEventSink
{
    Task OnCreatedAsync(Treatment treatment, CancellationToken ct = default);
    Task OnCreatedAsync(IReadOnlyList<Treatment> treatments, CancellationToken ct = default);
    Task OnUpdatedAsync(Treatment treatment, CancellationToken ct = default);
    Task OnDeletedAsync(Treatment treatment, CancellationToken ct = default);
}

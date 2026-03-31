using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.Treatments;

public class SignalRTreatmentEventSink : IDataEventSink<Treatment>
{
    private readonly ISignalRBroadcastService _broadcast;
    private readonly ILogger<SignalRTreatmentEventSink> _logger;
    private const string Collection = "treatments";

    public SignalRTreatmentEventSink(
        ISignalRBroadcastService broadcast,
        ILogger<SignalRTreatmentEventSink> logger)
    {
        _broadcast = broadcast;
        _logger = logger;
    }

    public async Task OnCreatedAsync(Treatment treatment, CancellationToken ct)
    {
        try
        {
            await _broadcast.BroadcastStorageCreateAsync(
                Collection, new { colName = Collection, doc = treatment });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast create for treatment {Id}", treatment.Id);
        }
    }

    public async Task OnCreatedAsync(IReadOnlyList<Treatment> treatments, CancellationToken ct)
    {
        foreach (var treatment in treatments)
            await OnCreatedAsync(treatment, ct);
    }

    public async Task OnUpdatedAsync(Treatment treatment, CancellationToken ct)
    {
        try
        {
            await _broadcast.BroadcastStorageUpdateAsync(
                Collection, new { colName = Collection, doc = treatment });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast update for treatment {Id}", treatment.Id);
        }
    }

    public async Task OnDeletedAsync(Treatment? treatment, CancellationToken ct)
    {
        try
        {
            await _broadcast.BroadcastStorageDeleteAsync(
                Collection, new { colName = Collection, doc = treatment });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to broadcast delete for treatment {Id}", treatment.Id);
        }
    }
}

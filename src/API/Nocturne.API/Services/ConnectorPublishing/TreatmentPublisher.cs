using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.ConnectorPublishing;

internal sealed class TreatmentPublisher : ITreatmentPublisher
{
    private readonly ITreatmentService _treatmentService;
    private readonly ILogger<TreatmentPublisher> _logger;

    public TreatmentPublisher(
        ITreatmentService treatmentService,
        ILogger<TreatmentPublisher> logger)
    {
        _treatmentService = treatmentService ?? throw new ArgumentNullException(nameof(treatmentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> PublishTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _treatmentService.CreateTreatmentsAsync(treatments, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish treatments for {Source}", source);
            return false;
        }
    }

    public async Task<DateTime?> GetLatestTreatmentTimestampAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        // TODO: Filter by source to support multi-connector catch-up. Currently returns global latest.
        var latest = (await _treatmentService.GetTreatmentsAsync(
                count: 1,
                skip: 0,
                cancellationToken: cancellationToken))
            .FirstOrDefault();

        if (latest == null)
            return null;

        if (!string.IsNullOrEmpty(latest.CreatedAt)
            && DateTime.TryParse(latest.CreatedAt, out var createdAt))
            return createdAt;

        if (latest.Mills > 0)
            return DateTimeOffset.FromUnixTimeMilliseconds(latest.Mills).UtcDateTime;

        return null;
    }
}

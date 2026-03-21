using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.ConnectorPublishing;

internal sealed class GlucosePublisher : IGlucosePublisher
{
    private readonly IEntryService _entryService;
    private readonly ISensorGlucoseRepository _sensorGlucoseRepository;
    private readonly IAlertOrchestrator _alertOrchestrator;
    private readonly ILogger<GlucosePublisher> _logger;

    public GlucosePublisher(
        IEntryService entryService,
        ISensorGlucoseRepository sensorGlucoseRepository,
        IAlertOrchestrator alertOrchestrator,
        ILogger<GlucosePublisher> logger)
    {
        _entryService = entryService ?? throw new ArgumentNullException(nameof(entryService));
        _sensorGlucoseRepository = sensorGlucoseRepository ?? throw new ArgumentNullException(nameof(sensorGlucoseRepository));
        _alertOrchestrator = alertOrchestrator ?? throw new ArgumentNullException(nameof(alertOrchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> PublishEntriesAsync(
        IEnumerable<Entry> entries,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _entryService.CreateEntriesAsync(entries, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish entries for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishSensorGlucoseAsync(
        IEnumerable<SensorGlucose> records,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var recordList = records.ToList();
            if (recordList.Count == 0) return true;

            await _sensorGlucoseRepository.BulkCreateAsync(recordList, cancellationToken);

            var latest = recordList.OrderByDescending(r => r.Timestamp).First();
            await _alertOrchestrator.EvaluateAndProcessSensorGlucoseAsync(
                [latest], null, cancellationToken);

            _logger.LogDebug("Published {Count} SensorGlucose records for {Source}", recordList.Count, source);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish SensorGlucose records for {Source}", source);
            return false;
        }
    }

    public async Task<DateTime?> GetLatestEntryTimestampAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        // TODO: Filter by source to support multi-connector catch-up. Currently returns global latest.
        var entry = await _entryService.GetCurrentEntryAsync(cancellationToken);
        if (entry == null)
            return null;

        if (entry.Date != default)
            return entry.Date;

        if (entry.Mills > 0)
            return DateTimeOffset.FromUnixTimeMilliseconds(entry.Mills).UtcDateTime;

        return null;
    }

    public async Task<DateTime?> GetLatestSensorGlucoseTimestampAsync(
        string source,
        CancellationToken cancellationToken = default)
    {
        return await _sensorGlucoseRepository.GetLatestTimestampAsync(source, cancellationToken);
    }
}

using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.Alerts;

public class AlertOrchestrator(
    IAlertRulesEngine rulesEngine,
    IAlertProcessingService processingService,
    ILogger<AlertOrchestrator> logger)
    : IAlertOrchestrator
{
    private const string DefaultUserId = "00000000-0000-0000-0000-000000000001";

    public async Task EvaluateAndProcessSensorGlucoseAsync(
        IEnumerable<SensorGlucose> readings,
        string? userId,
        CancellationToken cancellationToken = default
    )
    {
        var resolvedUserId = string.IsNullOrWhiteSpace(userId) ? DefaultUserId : userId;

        foreach (var reading in readings)
        {
            try
            {
                var alertEvents = await rulesEngine.EvaluateGlucoseData(
                    reading,
                    resolvedUserId,
                    cancellationToken
                );

                foreach (var alertEvent in alertEvents)
                {
                    try
                    {
                        await processingService.ProcessAlertEvent(alertEvent, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(
                            ex,
                            "Failed to process alert event {AlertType} for user {UserId}",
                            alertEvent.AlertType,
                            resolvedUserId
                        );
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to evaluate alert rules for SensorGlucose {ReadingId} and user {UserId}",
                    reading.Id,
                    resolvedUserId
                );
            }
        }
    }

    public async Task EvaluateAndProcessEntriesAsync(
        IEnumerable<Entry> entries,
        string? userId,
        CancellationToken cancellationToken = default
    )
    {
        var readings = entries.Select(EntryToSensorGlucose).ToList();
        await EvaluateAndProcessSensorGlucoseAsync(readings, userId, cancellationToken);
    }

    /// <summary>
    /// Converts a legacy Entry to a SensorGlucose record for alert evaluation.
    /// </summary>
    private static SensorGlucose EntryToSensorGlucose(Entry entry)
    {
        var mgdl = entry.Sgv ?? entry.Mgdl;
        var mills = entry.Date.HasValue
            ? new DateTimeOffset(entry.Date.Value, TimeSpan.Zero).ToUnixTimeMilliseconds()
            : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        GlucoseDirection? direction = null;
        if (!string.IsNullOrEmpty(entry.Direction) && Enum.TryParse<GlucoseDirection>(entry.Direction, true, out var parsed))
        {
            direction = parsed;
        }

        return new SensorGlucose
        {
            Id = Guid.TryParse(entry.Id, out var id) ? id : Guid.CreateVersion7(),
            Mills = mills,
            Mgdl = mgdl,
            Direction = direction,
            TrendRate = entry.Delta,
            DataSource = "legacy-entry",
        };
    }
}

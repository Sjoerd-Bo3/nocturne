using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services.ConnectorPublishing;

internal sealed class GlucosePublisher : IGlucosePublisher
{
    private readonly IEntryService _entryService;
    private readonly ILogger<GlucosePublisher> _logger;

    public GlucosePublisher(
        IEntryService entryService,
        ILogger<GlucosePublisher> logger)
    {
        _entryService = entryService ?? throw new ArgumentNullException(nameof(entryService));
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
}

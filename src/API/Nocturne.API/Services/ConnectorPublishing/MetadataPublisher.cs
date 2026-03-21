using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Services.ConnectorPublishing;

internal sealed class MetadataPublisher : IMetadataPublisher
{
    private const string DefaultUserId = "default";

    private readonly IProfileDataService _profileDataService;
    private readonly IFoodService _foodService;
    private readonly IConnectorFoodEntryService _connectorFoodEntryService;
    private readonly IActivityService _activityService;
    private readonly IStateSpanService _stateSpanService;
    private readonly SystemEventRepository _systemEventRepository;
    private readonly ILogger<MetadataPublisher> _logger;

    public MetadataPublisher(
        IProfileDataService profileDataService,
        IFoodService foodService,
        IConnectorFoodEntryService connectorFoodEntryService,
        IActivityService activityService,
        IStateSpanService stateSpanService,
        SystemEventRepository systemEventRepository,
        ILogger<MetadataPublisher> logger)
    {
        _profileDataService = profileDataService ?? throw new ArgumentNullException(nameof(profileDataService));
        _foodService = foodService ?? throw new ArgumentNullException(nameof(foodService));
        _connectorFoodEntryService = connectorFoodEntryService ?? throw new ArgumentNullException(nameof(connectorFoodEntryService));
        _activityService = activityService ?? throw new ArgumentNullException(nameof(activityService));
        _stateSpanService = stateSpanService ?? throw new ArgumentNullException(nameof(stateSpanService));
        _systemEventRepository = systemEventRepository ?? throw new ArgumentNullException(nameof(systemEventRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> PublishProfilesAsync(
        IEnumerable<Profile> profiles,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _profileDataService.CreateProfilesAsync(profiles, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish profiles for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishFoodAsync(
        IEnumerable<Food> foods,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _foodService.CreateFoodAsync(foods, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish food for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishConnectorFoodEntriesAsync(
        IEnumerable<ConnectorFoodEntryImport> entries,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _connectorFoodEntryService.ImportAsync(
                DefaultUserId,
                entries,
                cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish connector food entries for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishActivityAsync(
        IEnumerable<Activity> activities,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _activityService.CreateActivitiesAsync(activities, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish activities for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishStateSpansAsync(
        IEnumerable<StateSpan> stateSpans,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var span in stateSpans)
            {
                await _stateSpanService.UpsertStateSpanAsync(span, cancellationToken);
            }
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish state spans for {Source}", source);
            return false;
        }
    }

    public async Task<bool> PublishSystemEventsAsync(
        IEnumerable<SystemEvent> systemEvents,
        string source,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _systemEventRepository.BulkUpsertAsync(systemEvents, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish system events for {Source}", source);
            return false;
        }
    }
}

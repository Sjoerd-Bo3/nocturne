using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Connectors.Glooko.Configurations;
using Nocturne.Connectors.Glooko.Mappers;
using Nocturne.Connectors.Glooko.Models;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Glooko.Services;

/// <summary>
///     Connector service for Glooko data source
///     Based on the original nightscout-connect Glooko implementation
/// </summary>
public class GlookoConnectorService : BaseConnectorService<GlookoConnectorConfiguration>
{
    private readonly GlookoConnectorConfiguration _config;
    private readonly IRateLimitingStrategy _rateLimitingStrategy;
    private readonly IRetryDelayStrategy _retryDelayStrategy;
    private readonly GlookoProfileMapper _profileMapper;
    private readonly GlookoSensorGlucoseMapper _sensorGlucoseMapper;
    private readonly GlookoStateSpanMapper _stateSpanMapper;
    private readonly GlookoSystemEventMapper _systemEventMapper;
    private readonly GlookoTempBasalMapper _tempBasalMapper;
    private readonly GlookoTimeMapper _timeMapper;
    private readonly GlookoAuthTokenProvider _tokenProvider;
    private readonly GlookoV4TreatmentMapper _v4TreatmentMapper;

    public GlookoConnectorService(
        HttpClient httpClient,
        IOptions<GlookoConnectorConfiguration> config,
        ILogger<GlookoConnectorService> logger,
        IRetryDelayStrategy retryDelayStrategy,
        IRateLimitingStrategy rateLimitingStrategy,
        GlookoAuthTokenProvider tokenProvider,
        IConnectorPublisher? publisher = null
    )
        : base(httpClient, logger, publisher)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _retryDelayStrategy =
            retryDelayStrategy ?? throw new ArgumentNullException(nameof(retryDelayStrategy));
        _rateLimitingStrategy =
            rateLimitingStrategy
            ?? throw new ArgumentNullException(nameof(rateLimitingStrategy));
        _tokenProvider =
            tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _timeMapper = new GlookoTimeMapper(_config, logger);
        _sensorGlucoseMapper = new GlookoSensorGlucoseMapper(_config, ConnectorSource, _timeMapper, logger);
        _v4TreatmentMapper = new GlookoV4TreatmentMapper(ConnectorSource, _timeMapper, logger);
        _stateSpanMapper = new GlookoStateSpanMapper(ConnectorSource, _timeMapper, logger);
        _tempBasalMapper = new GlookoTempBasalMapper(ConnectorSource, _timeMapper, logger);
        _systemEventMapper = new GlookoSystemEventMapper(ConnectorSource, _timeMapper, logger);
        _profileMapper = new GlookoProfileMapper(ConnectorSource, logger);
    }

    public override string ServiceName => "Glooko";
    protected override string ConnectorSource => DataSources.GlookoConnector;

    public override List<SyncDataType> SupportedDataTypes =>
    [
        SyncDataType.Glucose,
        SyncDataType.Boluses,
        SyncDataType.CarbIntake,
        SyncDataType.StateSpans,
        SyncDataType.DeviceEvents,
        SyncDataType.Profiles
    ];

    public override async Task<bool> AuthenticateAsync()
    {
        var token = await _tokenProvider.GetValidTokenAsync();
        if (token == null)
        {
            TrackFailedRequest("Failed to get valid token");
            return false;
        }

        TrackSuccessfulRequest();
        return true;
    }

    protected override async Task<SyncResult> PerformSyncInternalAsync(
        SyncRequest request,
        GlookoConnectorConfiguration config,
        CancellationToken cancellationToken,
        ISyncProgressReporter? progressReporter = null
    )
    {
        var result = new SyncResult
        {
            Success = true,
            Message = "Sync completed successfully",
            StartTime = DateTime.UtcNow
        };

        try
        {
            if (IsSessionExpired())
                if (!await AuthenticateAsync())
                {
                    result.Success = false;
                    result.Message = "Authentication failed";
                    result.Errors.Add("Authentication failed");
                    return result;
                }

            // Compute active types: intersection of requested and enabled types
            if (!request.DataTypes.Any())
                request.DataTypes = SupportedDataTypes;
            var enabledTypes = config.GetEnabledDataTypes(SupportedDataTypes);
            var activeTypes = request.DataTypes.Where(t => enabledTypes.Contains(t)).ToHashSet();

            // Glooko fetches everything in one go, so determine the earliest 'From' date needed
            var from = request.From;

            var batchData = await FetchBatchDataAsync(from);

            if (batchData == null)
            {
                result.Success = false;
                result.Message = "Failed to fetch data";
                result.Errors.Add("No data returned from Glooko");
                return result;
            }

            // Fetch V3 data once upfront if needed for any data type
            GlookoV3GraphResponse? v3Data = null;
            var needsV3Data = _config.UseV3Api && (
                activeTypes.Contains(SyncDataType.Boluses) ||
                activeTypes.Contains(SyncDataType.CarbIntake) ||
                activeTypes.Contains(SyncDataType.StateSpans) ||
                activeTypes.Contains(SyncDataType.DeviceEvents) ||
                (_config.V3IncludeCgmBackfill && activeTypes.Contains(SyncDataType.Glucose))
            );

            if (needsV3Data)
            {
                try
                {
                    _logger.LogInformation(
                        "[{ConnectorSource}] Fetching additional data from v3 API...",
                        ConnectorSource
                    );
                    v3Data = await FetchV3GraphDataAsync(from);
                }
                catch (Exception v3Ex)
                {
                    _logger.LogWarning(
                        v3Ex,
                        "[{ConnectorSource}] V3 API fetch failed, continuing with v2 data only",
                        ConnectorSource
                    );
                }
            }

            // 1. Process Glucose
            if (activeTypes.Contains(SyncDataType.Glucose))
            {
                var sensorGlucose = _sensorGlucoseMapper.TransformBatchDataToSensorGlucose(batchData).ToList();
                if (sensorGlucose.Count > 0)
                {
                    var success = await PublishSensorGlucoseDataAsync(sensorGlucose, config, cancellationToken);
                    if (success)
                    {
                        result.ItemsSynced[SyncDataType.Glucose] = sensorGlucose.Count;
                        result.LastEntryTimes[SyncDataType.Glucose] = DateTimeOffset
                            .FromUnixTimeMilliseconds(sensorGlucose.Max(s => s.Mills)).UtcDateTime;
                    }
                }

                // V3 CGM backfill
                if (_config.V3IncludeCgmBackfill && v3Data != null)
                {
                    var v3Glucose = _sensorGlucoseMapper.TransformV3ToSensorGlucose(v3Data, _meterUnits).ToList();
                    if (v3Glucose.Count > 0)
                    {
                        await PublishSensorGlucoseDataAsync(v3Glucose, config, cancellationToken);
                        _logger.LogInformation(
                            "[{ConnectorSource}] Published {Count} CGM backfill sensor glucose from v3",
                            ConnectorSource, v3Glucose.Count);
                    }
                }
            }

            // 2. Process Treatments (boluses, carb intake)
            var allBoluses = new List<Bolus>();
            var allCarbs = new List<CarbIntake>();
            var allDeviceEvents = new List<DeviceEvent>();

            // Prefer V3 data for boluses/carbs when available (V2 and V3 return
            // the same records with different shapes, so using both causes duplicates).
            // V2 standalone Foods have no V3 equivalent, so always include those.
            if (_config.UseV3Api && v3Data != null)
            {
                var (v3Boluses, v3BolusCarbIntakes) = _v4TreatmentMapper.MapV3Boluses(v3Data);
                allBoluses.AddRange(v3Boluses);
                allCarbs.AddRange(v3BolusCarbIntakes);

                // V2 standalone food records have no V3 equivalent
                var v2Foods = _v4TreatmentMapper.MapFoods(batchData);
                allCarbs.AddRange(v2Foods);

                var v3DeviceEvents = _v4TreatmentMapper.MapV3DeviceEvents(v3Data);
                allDeviceEvents.AddRange(v3DeviceEvents);
            }
            else
            {
                var (v2Boluses, v2Carbs) = _v4TreatmentMapper.MapBatchData(batchData);
                allBoluses.AddRange(v2Boluses);
                allCarbs.AddRange(v2Carbs);
            }

            // Publish boluses
            if (activeTypes.Contains(SyncDataType.Boluses) && allBoluses.Count > 0)
            {
                var success = await PublishBolusDataAsync(allBoluses, config, cancellationToken);
                if (success)
                {
                    result.ItemsSynced[SyncDataType.Boluses] = allBoluses.Count;
                    _logger.LogInformation("[{ConnectorSource}] Published {Count} boluses", ConnectorSource, allBoluses.Count);
                }
            }

            // Publish carb intakes
            if (activeTypes.Contains(SyncDataType.CarbIntake) && allCarbs.Count > 0)
            {
                var success = await PublishCarbIntakeDataAsync(allCarbs, config, cancellationToken);
                if (success)
                {
                    result.ItemsSynced[SyncDataType.CarbIntake] = allCarbs.Count;
                    _logger.LogInformation("[{ConnectorSource}] Published {Count} carb intakes", ConnectorSource, allCarbs.Count);
                }
            }

            // 3. Process DeviceEvents (reservoir/site changes + pump alarms from V3)
            if (activeTypes.Contains(SyncDataType.DeviceEvents))
            {
                var deviceEventCount = 0;

                if (allDeviceEvents.Count > 0)
                {
                    var success = await PublishDeviceEventDataAsync(allDeviceEvents, config, cancellationToken);
                    if (success)
                    {
                        deviceEventCount += allDeviceEvents.Count;
                        _logger.LogInformation("[{ConnectorSource}] Published {Count} device events", ConnectorSource, allDeviceEvents.Count);
                    }
                }

                if (v3Data != null)
                {
                    var systemEvents = _systemEventMapper.TransformV3ToSystemEvents(v3Data);
                    if (systemEvents.Any())
                    {
                        var eventSuccess = await PublishSystemEventDataAsync(systemEvents, config, cancellationToken);
                        if (eventSuccess)
                        {
                            deviceEventCount += systemEvents.Count;
                            _logger.LogInformation("[{ConnectorSource}] Published {Count} system events from v3", ConnectorSource, systemEvents.Count);
                        }
                    }
                }

                if (deviceEventCount > 0)
                    result.ItemsSynced[SyncDataType.DeviceEvents] = deviceEventCount;
            }

            // 4. Process StateSpans (pump modes, profiles) and TempBasals
            if (activeTypes.Contains(SyncDataType.StateSpans))
            {
                var tempBasalCount = 0;

                // V3 state spans (pump modes, profiles)
                if (v3Data != null)
                {
                    var stateSpans = _stateSpanMapper.TransformV3ToStateSpans(v3Data);
                    if (stateSpans.Any())
                    {
                        var stateSpanSuccess = await PublishStateSpanDataAsync(stateSpans, config, cancellationToken);
                        if (stateSpanSuccess)
                            _logger.LogInformation(
                                "[{ConnectorSource}] Published {Count} state spans from v3",
                                ConnectorSource, stateSpans.Count);
                    }

                    // V3 temp basals
                    var v3TempBasals = _tempBasalMapper.TransformV3ToTempBasals(v3Data);
                    if (v3TempBasals.Any())
                    {
                        var tbSuccess = await PublishTempBasalDataAsync(v3TempBasals, config, cancellationToken);
                        if (tbSuccess)
                        {
                            tempBasalCount += v3TempBasals.Count;
                            _logger.LogInformation(
                                "[{ConnectorSource}] Published {Count} temp basals from v3",
                                ConnectorSource, v3TempBasals.Count);
                        }
                    }
                }

                // V2 state spans (suspend pump modes)
                var v2StateSpans = _stateSpanMapper.TransformV2ToStateSpans(batchData);
                if (v2StateSpans.Any())
                {
                    var v2StateSpanSuccess = await PublishStateSpanDataAsync(v2StateSpans, config, cancellationToken);
                    if (v2StateSpanSuccess)
                        _logger.LogInformation(
                            "[{ConnectorSource}] Published {Count} state spans from v2",
                            ConnectorSource, v2StateSpans.Count);
                }

                // V2 temp basals
                var v2TempBasals = _tempBasalMapper.TransformV2ToTempBasals(batchData);
                if (v2TempBasals.Any())
                {
                    var v2TbSuccess = await PublishTempBasalDataAsync(v2TempBasals, config, cancellationToken);
                    if (v2TbSuccess)
                    {
                        tempBasalCount += v2TempBasals.Count;
                        _logger.LogInformation(
                            "[{ConnectorSource}] Published {Count} temp basals from v2",
                            ConnectorSource, v2TempBasals.Count);
                    }
                }

                if (tempBasalCount > 0)
                    result.ItemsSynced[SyncDataType.StateSpans] = tempBasalCount;
            }

            // 5. Process Profiles (from V3 devices_and_settings)
            if (activeTypes.Contains(SyncDataType.Profiles))
                try
                {
                    var deviceSettings = await FetchV3DeviceSettingsAsync();
                    if (deviceSettings != null)
                    {
                        var profiles = _profileMapper.TransformDeviceSettingsToProfiles(deviceSettings);
                        if (profiles.Any())
                        {
                            var profileSuccess = await PublishProfileDataAsync(
                                profiles,
                                config,
                                cancellationToken
                            );
                            if (profileSuccess)
                            {
                                result.ItemsSynced[SyncDataType.Profiles] = profiles.Count;
                                _logger.LogInformation(
                                    "[{ConnectorSource}] Published {Count} profiles from device settings",
                                    ConnectorSource,
                                    profiles.Count
                                );
                            }
                        }
                    }
                }
                catch (Exception profileEx)
                {
                    _logger.LogWarning(
                        profileEx,
                        "[{ConnectorSource}] Failed to fetch/publish profile data",
                        ConnectorSource
                    );
                }

            result.EndTime = DateTime.UtcNow;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Glooko batch sync");
            result.Success = false;
            result.Message = "Sync failed with exception";
            result.Errors.Add(ex.Message);
            result.EndTime = DateTime.UtcNow;
            return result;
        }
    }

    /// <summary>
    ///     Fetch comprehensive batch data from all Glooko endpoints
    ///     This matches the legacy implementation's dataFromSession method
    /// </summary>
    public async Task<GlookoBatchData?> FetchBatchDataAsync(DateTime? since = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_tokenProvider.SessionCookie))
                throw new InvalidOperationException(
                    "Not authenticated with Glooko. Call AuthenticateAsync first."
                );
            if (_tokenProvider.UserData?.UserLogin?.GlookoCode == null)
            {
                _logger.LogWarning("Missing Glooko user code, cannot fetch data");
                return null;
            }

            // Calculate date range - fetch from specified date or last 24 hours
            var fromDate = since ?? DateTime.UtcNow.AddDays(-1);
            var toDate = DateTime.UtcNow;

            _logger.LogInformation(
                $"Fetching comprehensive Glooko data from {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}"
            );

            var batchData = new GlookoBatchData();

            // Define endpoints and their handlers
            var endpointDefinitions = new[]
            {
                new
                {
                    Endpoint = "/api/v2/foods",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("foods", out var element))
                            batchData.Foods =
                                JsonSerializer.Deserialize<GlookoFood[]>(element.GetRawText())
                                ?? Array.Empty<GlookoFood>();
                    })
                },
                new
                {
                    Endpoint = "/api/v2/pumps/scheduled_basals",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("scheduledBasals", out var element))
                            batchData.ScheduledBasals =
                                JsonSerializer.Deserialize<GlookoBasal[]>(element.GetRawText())
                                ?? Array.Empty<GlookoBasal>();
                    })
                },
                new
                {
                    Endpoint = "/api/v2/pumps/normal_boluses",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("normalBoluses", out var element))
                            batchData.NormalBoluses =
                                JsonSerializer.Deserialize<GlookoBolus[]>(element.GetRawText())
                                ?? Array.Empty<GlookoBolus>();
                    })
                },
                new
                {
                    Endpoint = "/api/v2/cgm/readings",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("readings", out var element))
                            batchData.Readings =
                                JsonSerializer.Deserialize<GlookoCgmReading[]>(
                                    element.GetRawText()
                                ) ?? Array.Empty<GlookoCgmReading>();
                    })
                },
                new
                {
                    Endpoint = "/api/v2/pumps/suspend_basals",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("suspendBasals", out var element))
                            batchData.SuspendBasals =
                                JsonSerializer.Deserialize<GlookoSuspendBasal[]>(
                                    element.GetRawText()
                                ) ?? Array.Empty<GlookoSuspendBasal>();
                    })
                },
                new
                {
                    Endpoint = "/api/v2/pumps/temporary_basals",
                    Handler = new Action<JsonElement>(json =>
                    {
                        if (json.TryGetProperty("temporaryBasals", out var element))
                            batchData.TempBasals =
                                JsonSerializer.Deserialize<GlookoTempBasal[]>(
                                    element.GetRawText()
                                ) ?? Array.Empty<GlookoTempBasal>();
                    })
                }
            };

            // Fetch endpoints sequentially with rate limiting
            for (var i = 0; i < endpointDefinitions.Length; i++)
            {
                var def = endpointDefinitions[i];
                var url = ConstructGlookoUrl(def.Endpoint, fromDate, toDate);

                // Apply rate limiting strategy
                await _rateLimitingStrategy.ApplyDelayAsync(i);

                try
                {
                    var result = await FetchFromGlookoEndpointWithRetry(url);
                    if (result.HasValue)
                        try
                        {
                            def.Handler(result.Value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Error parsing data from {Endpoint}",
                                def.Endpoint
                            );
                        }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to fetch from {Url}. Continuing with other endpoints.",
                        url
                    );
                }
            }

            // Log a summary of fetched data with source identifier
            _logger.LogInformation(
                "[{ConnectorSource}] Fetched Glooko batch data summary: "
                + "Readings={ReadingsCount}, Foods={FoodsCount}, "
                + "NormalBoluses={BolusCount}, TempBasals={TempBasalCount}, "
                + "ScheduledBasals={ScheduledBasalCount}, Suspends={SuspendCount}",
                ConnectorSource,
                batchData.Readings?.Length ?? 0,
                batchData.Foods?.Length ?? 0,
                batchData.NormalBoluses?.Length ?? 0,
                batchData.TempBasals?.Length ?? 0,
                batchData.ScheduledBasals?.Length ?? 0,
                batchData.SuspendBasals?.Length ?? 0
            );

            return batchData;
        }
        catch (InvalidOperationException)
        {
            // Re-throw authentication-related exceptions
            throw;
        }
        catch (HttpRequestException)
        {
            // Re-throw HTTP-related exceptions (including rate limiting)
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko batch data");
            return null;
        }
    }

    private string ConstructGlookoUrl(string endpoint, DateTime startDate, DateTime endDate)
    {
        var patientCode = _tokenProvider.UserData?.UserLogin?.GlookoCode;

        // Add the required parameters matching the legacy implementation
        var lastGuid = "1e0c094e-1e54-4a4f-8e6a-f94484b53789"; // hardcoded as per legacy
        var maxCount = Math.Max(1, (int)Math.Ceiling((endDate - startDate).TotalMinutes / 5)); // 5-minute intervals

        return
            $"{endpoint}?patient={patientCode}&startDate={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}&endDate={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}&lastGuid={lastGuid}&lastUpdatedAt={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}&limit={maxCount}";
    }

    private async Task<JsonElement?> FetchFromGlookoEndpoint(string url)
    {
        try
        {
            _logger.LogDebug("GLOOKO FETCHER LOADING {Url}", url);

            var request = new HttpRequestMessage(HttpMethod.Get, url);

            // Add required headers (matching legacy implementation)
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "application/json, text/plain, */*"
            );
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            request.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.5 Safari/605.1.15"
            );
            request.Headers.TryAddWithoutValidation("Referer", "https://eu.my.glooko.com/");
            request.Headers.TryAddWithoutValidation("Origin", "https://eu.my.glooko.com");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-GB,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Cookie", _tokenProvider.SessionCookie);
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");

            var response = await _httpClient.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                // Read response as bytes first to handle compression properly
                var responseBytes = await response.Content.ReadAsByteArrayAsync();

                // Decompress if needed (check for gzip magic number 0x1F 0x8B)
                string responseJson;
                if (
                    responseBytes.Length >= 2
                    && responseBytes[0] == 0x1F
                    && responseBytes[1] == 0x8B
                )
                {
                    using var compressedStream = new MemoryStream(responseBytes);
                    using var gzipStream = new GZipStream(
                        compressedStream,
                        CompressionMode.Decompress
                    );
                    using var decompressedStream = new MemoryStream();
                    await gzipStream.CopyToAsync(decompressedStream);
                    responseJson = Encoding.UTF8.GetString(decompressedStream.ToArray());
                }
                else
                {
                    responseJson = Encoding.UTF8.GetString(responseBytes);
                }

                return JsonSerializer.Deserialize<JsonElement>(responseJson);
            }

            if (response.StatusCode == HttpStatusCode.UnprocessableEntity) // 422
            {
                _logger.LogWarning("Rate limited (422) fetching from {Url}", url);
                throw new HttpRequestException("422 UnprocessableEntity - Rate limited");
            }

            _logger.LogWarning("Failed to fetch from {Url}: {StatusCode}", url, response.StatusCode);
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.StatusCode}"
            );
        }
        catch (HttpRequestException)
        {
            // Re-throw HTTP exceptions (including rate limiting) to be handled by retry logic
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching from {Url}", url);
            throw new HttpRequestException($"Request failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Fetch from Glooko endpoint with retry logic and exponential backoff
    ///     Implements the legacy rate limiting strategy to avoid 422 errors
    /// </summary>
    private async Task<JsonElement?> FetchFromGlookoEndpointWithRetry(
        string url,
        int maxRetries = 3
    )
    {
        HttpRequestException? lastException = null;

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                var result = await FetchFromGlookoEndpoint(url);
                if (result.HasValue) return result;

                // If we get here, the request failed but didn't throw
                _logger.LogWarning("Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("422"))
            {
                lastException = ex;
                _logger.LogWarning("Rate limited (422) on attempt {AttemptNumber} for {Url}", attempt + 1, url);
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
                _logger.LogError(ex, "Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Attempt {AttemptNumber} failed for {Url}", attempt + 1, url);
                lastException = new HttpRequestException($"Request failed: {ex.Message}", ex);
            } // Don't delay after the last attempt

            if (attempt < maxRetries - 1)
            {
                _logger.LogInformation("Applying retry backoff before retry {RetryNumber}", attempt + 2);
                await _retryDelayStrategy.ApplyRetryDelayAsync(attempt);
            }
        }

        _logger.LogError("All {MaxRetries} attempts failed for {Url}", maxRetries, url);

        // Throw the last exception if we have one, otherwise throw a generic exception
        if (lastException != null) throw lastException;
        throw new HttpRequestException($"All {maxRetries} attempts failed for {url}");
    }

    private bool IsSessionExpired()
    {
        return string.IsNullOrEmpty(_tokenProvider.SessionCookie);
    }

    #region V3 API Methods

    private string? _meterUnits;

    /// <summary>
    ///     Fetch user profile from v3 API to get meter units setting
    /// </summary>
    public async Task<GlookoV3UsersResponse?> FetchV3UserProfileAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_tokenProvider.SessionCookie))
                throw new InvalidOperationException(
                    "Not authenticated with Glooko. Call AuthenticateAsync first."
                );

            var url = "/api/v3/session/users";
            _logger.LogDebug("Fetching Glooko v3 user profile from {Url}", url);

            var result = await FetchFromGlookoEndpoint(url);
            if (result.HasValue)
            {
                var profile = JsonSerializer.Deserialize<GlookoV3UsersResponse>(
                    result.Value.GetRawText()
                );
                if (profile?.CurrentUser != null)
                {
                    _meterUnits = profile.CurrentUser.MeterUnits;
                    _logger.LogInformation(
                        "[{ConnectorSource}] User profile loaded. MeterUnits: {Units}",
                        ConnectorSource,
                        _meterUnits
                    );
                }

                return profile;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 user profile");
            return null;
        }
    }

    /// <summary>
    ///     Fetch data from v3 graph/data API - single call for all data types
    /// </summary>
    public async Task<GlookoV3GraphResponse?> FetchV3GraphDataAsync(DateTime? since = null)
    {
        try
        {
            if (string.IsNullOrEmpty(_tokenProvider.SessionCookie))
                throw new InvalidOperationException(
                    "Not authenticated with Glooko. Call AuthenticateAsync first."
                );

            if (_tokenProvider.UserData?.UserLogin?.GlookoCode == null)
            {
                _logger.LogWarning("Missing Glooko user code, cannot fetch v3 data");
                return null;
            }

            // Ensure we have meter units
            if (string.IsNullOrEmpty(_meterUnits)) await FetchV3UserProfileAsync();

            var fromDate = since ?? DateTime.UtcNow.AddDays(-1);
            var toDate = DateTime.UtcNow;

            var url = ConstructV3GraphUrl(fromDate, toDate);
            _logger.LogInformation(
                "[{ConnectorSource}] Fetching v3 graph data from {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}",
                ConnectorSource,
                fromDate,
                toDate
            );

            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (result.HasValue)
            {
                var graphData = JsonSerializer.Deserialize<GlookoV3GraphResponse>(
                    result.Value.GetRawText()
                );

                if (graphData?.Series != null)
                    _logger.LogInformation(
                        "[{ConnectorSource}] Fetched v3 graph data: "
                        + "AutomaticBolus={AutoBolus}, DeliveredBolus={Bolus}, "
                        + "PumpAlarm={Alarms}, ReservoirChange={Reservoir}, SetSiteChange={SetSite}, "
                        + "CgmReadings={Cgm}",
                        ConnectorSource,
                        graphData.Series.AutomaticBolus?.Length ?? 0,
                        graphData.Series.DeliveredBolus?.Length ?? 0,
                        graphData.Series.PumpAlarm?.Length ?? 0,
                        graphData.Series.ReservoirChange?.Length ?? 0,
                        graphData.Series.SetSiteChange?.Length ?? 0,
                        (graphData.Series.CgmHigh?.Length ?? 0)
                        + (graphData.Series.CgmNormal?.Length ?? 0)
                        + (graphData.Series.CgmLow?.Length ?? 0)
                    );

                return graphData;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 graph data");
            return null;
        }
    }

    /// <summary>
    ///     Construct URL for v3 graph/data endpoint with all requested series
    /// </summary>
    private string ConstructV3GraphUrl(DateTime startDate, DateTime endDate)
    {
        var patientCode = _tokenProvider.UserData?.UserLogin?.GlookoCode;

        // Series to request
        var series = new[]
        {
            "automaticBolus",
            "deliveredBolus",
            "injectionBolus",
            "pumpAlarm",
            "reservoirChange",
            "setSiteChange",
            "carbAll",
            "scheduledBasal",
            "temporaryBasal",
            "suspendBasal",
            "lgsPlgs",
            "profileChange"
        };

        // Add CGM series if backfill is enabled
        if (_config.V3IncludeCgmBackfill) series = series.Concat(new[] { "cgmHigh", "cgmNormal", "cgmLow" }).ToArray();

        var seriesParams = string.Join("&", series.Select(s => $"series[]={s}"));

        return $"/api/v3/graph/data?patient={patientCode}"
               + $"&startDate={startDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
               + $"&endDate={endDate:yyyy-MM-ddTHH:mm:ss.fffZ}"
               + $"&{seriesParams}"
               + "&locale=en&insulinTooltips=false&filterBgReadings=false&splitByDay=false";
    }

    /// <summary>
    ///     Fetch pump device settings from the v3 devices_and_settings API
    /// </summary>
    public async Task<GlookoV3DeviceSettingsResponse?> FetchV3DeviceSettingsAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_tokenProvider.SessionCookie))
                throw new InvalidOperationException(
                    "Not authenticated with Glooko. Call AuthenticateAsync first."
                );

            if (_tokenProvider.UserData?.UserLogin?.GlookoCode == null)
            {
                _logger.LogWarning("Missing Glooko user code, cannot fetch device settings");
                return null;
            }

            var patientCode = _tokenProvider.UserData.UserLogin.GlookoCode;
            var url = $"/api/v3/devices_and_settings?patient={patientCode}";

            _logger.LogInformation(
                "[{ConnectorSource}] Fetching device settings from v3 API",
                ConnectorSource
            );

            var result = await FetchFromGlookoEndpointWithRetry(url);
            if (result.HasValue)
            {
                var settings = JsonSerializer.Deserialize<GlookoV3DeviceSettingsResponse>(
                    result.Value.GetRawText()
                );

                var pumpCount = settings?.DeviceSettings?.Pumps?.Count ?? 0;
                var snapshotCount = settings?.DeviceSettings?.Pumps?.Values
                    .Sum(p => p.Count) ?? 0;

                _logger.LogInformation(
                    "[{ConnectorSource}] Fetched device settings: {PumpCount} pumps, {SnapshotCount} settings snapshots",
                    ConnectorSource,
                    pumpCount,
                    snapshotCount
                );

                return settings;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Glooko v3 device settings");
            return null;
        }
    }

    #endregion
}

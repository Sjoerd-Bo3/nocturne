using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Background service that detects compression lows in overnight glucose data
/// </summary>
public class CompressionLowDetectionService : BackgroundService, ICompressionLowDetectionService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CompressionLowDetectionService> _logger;

    // Detection configuration - sleep hours are now read from settings
    private const int DetectionDelayMinutes = 15;
    internal const double MinDropRateMgDlPerMin = 2.0;
    internal const int MinDropDurationMinutes = 10;
    internal const double NadirThresholdMgDl = 70.0;
    internal const double RecoveryPercentage = 0.20;
    internal const int MaxRecoveryMinutes = 60;
    internal const double MinConfidenceThreshold = 0.5;
    private const int StartTrimMinutes = 5;

    public CompressionLowDetectionService(
        IServiceProvider serviceProvider,
        ILogger<CompressionLowDetectionService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Compression Low Detection Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Compute the next run time by finding the earliest wake time across all tenants.
                // Each tenant has its own sleep schedule and timezone, so we iterate them to find
                // the soonest upcoming wake-time + detection delay.
                var delay = await ComputeNextRunDelayAsync(stoppingToken);
                await Task.Delay(delay, stoppingToken);

                // Run detection for all tenants
                await RunForAllTenantsAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in compression low detection cycle");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        _logger.LogInformation("Compression Low Detection Service stopped");
    }

    /// <summary>
    /// Iterates all active tenants and computes the earliest next detection time
    /// based on each tenant's wake-time setting and timezone. Returns the delay
    /// until that time. Falls back to a 1-hour delay if no tenants are configured.
    /// </summary>
    private async Task<TimeSpan> ComputeNextRunDelayAsync(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        DateTime? earliestNextRunUtc = null;
        string? earliestTimezoneId = null;

        using var lookupScope = _serviceProvider.CreateScope();
        var factory = lookupScope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var lookupContext = await factory.CreateDbContextAsync(cancellationToken);
        var tenants = await lookupContext.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Slug, t.DisplayName })
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
                tenantAccessor.SetTenant(new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, true));

                var uiSettingsService = scope.ServiceProvider.GetRequiredService<IUISettingsService>();
                var profileDataService = scope.ServiceProvider.GetRequiredService<IProfileDataService>();
                var entryService = scope.ServiceProvider.GetRequiredService<IEntryService>();

                var settings = await uiSettingsService.GetSettingsAsync(cancellationToken);
                var sleepSchedule = settings.DataQuality.SleepSchedule;
                var wakeTimeHour = sleepSchedule.WakeTimeHour;
                var lastNightGuess = DateOnly.FromDateTime(nowUtc.AddDays(-1));
                var userTimeZone = ResolveTimeZone(sleepSchedule.Timezone)
                    ?? await GetUserTimeZoneFromProfileAsync(profileDataService, cancellationToken)
                    ?? await InferTimeZoneFromEntriesAsync(entryService, lastNightGuess, cancellationToken)
                    ?? TimeZoneInfo.Utc;

                var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, userTimeZone);
                var nextRunLocal = nowLocal.Date.AddHours(wakeTimeHour).AddMinutes(DetectionDelayMinutes);

                if (nowLocal >= nextRunLocal)
                    nextRunLocal = nextRunLocal.AddDays(1);

                var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, userTimeZone);

                if (earliestNextRunUtc == null || nextRunUtc < earliestNextRunUtc)
                {
                    earliestNextRunUtc = nextRunUtc;
                    earliestTimezoneId = userTimeZone.Id;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to compute next run time for tenant {TenantSlug}, skipping", tenant.Slug);
            }
        }

        if (earliestNextRunUtc == null)
        {
            _logger.LogDebug("No active tenants found for scheduling; retrying in 1 hour");
            return TimeSpan.FromHours(1);
        }

        var delay = earliestNextRunUtc.Value - nowUtc;
        _logger.LogDebug(
            "Next compression low detection scheduled for {NextRunUtc:u} (earliest tenant timezone: {Timezone})",
            earliestNextRunUtc.Value, earliestTimezoneId);

        return delay;
    }

    private async Task RunForAllTenantsAsync(CancellationToken cancellationToken)
    {
        // Lookup active tenants using unfiltered context
        using var lookupScope = _serviceProvider.CreateScope();
        var factory = lookupScope.ServiceProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var lookupContext = await factory.CreateDbContextAsync(cancellationToken);
        var tenants = await lookupContext.Tenants.AsNoTracking()
            .Where(t => t.IsActive)
            .Select(t => new { t.Id, t.Slug, t.DisplayName })
            .ToListAsync(cancellationToken);

        foreach (var tenant in tenants)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var tenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
                tenantAccessor.SetTenant(new TenantContext(tenant.Id, tenant.Slug, tenant.DisplayName, true));

                // Determine "last night" in the user's local timezone
                var profileDataService = scope.ServiceProvider.GetRequiredService<IProfileDataService>();
                var entryService = scope.ServiceProvider.GetRequiredService<IEntryService>();
                var uiSettingsService = scope.ServiceProvider.GetRequiredService<IUISettingsService>();
                var settings = await uiSettingsService.GetSettingsAsync(cancellationToken);
                var lastNightGuess = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                var userTimeZone = ResolveTimeZone(settings.DataQuality.SleepSchedule.Timezone)
                    ?? await GetUserTimeZoneFromProfileAsync(profileDataService, cancellationToken)
                    ?? await InferTimeZoneFromEntriesAsync(entryService, lastNightGuess, cancellationToken)
                    ?? TimeZoneInfo.Utc;
                var detectionTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, userTimeZone);
                var lastNight = DateOnly.FromDateTime(detectionTimeLocal.AddDays(-1));

                await DetectForNightInternalAsync(lastNight, scope.ServiceProvider, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during compression low detection for tenant {TenantSlug}", tenant.Slug);
            }
        }
    }

    public async Task<int> DetectForNightAsync(
        DateOnly nightOf,
        CancellationToken cancellationToken = default)
    {
        using var scope = _serviceProvider.CreateScope();

        // When called from an HTTP endpoint, the tenant context lives in the request scope.
        // Propagate it to the new scope so tenant-scoped services (EntryService, ProfileDataService, etc.) work.
        var httpContextAccessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
        var requestTenantAccessor = httpContextAccessor?.HttpContext?.RequestServices.GetService<ITenantAccessor>();
        if (requestTenantAccessor is { IsResolved: true, Context: not null })
        {
            var scopedTenantAccessor = scope.ServiceProvider.GetRequiredService<ITenantAccessor>();
            scopedTenantAccessor.SetTenant(requestTenantAccessor.Context);
        }

        return await DetectForNightInternalAsync(nightOf, scope.ServiceProvider, cancellationToken);
    }

    private async Task<int> DetectForNightInternalAsync(
        DateOnly nightOf,
        IServiceProvider scopedProvider,
        CancellationToken cancellationToken)
    {
        var repository = scopedProvider.GetRequiredService<ICompressionLowRepository>();
        var entryService = scopedProvider.GetRequiredService<IEntryService>();
        var treatmentService = scopedProvider.GetRequiredService<ITreatmentService>();
        var notificationService = scopedProvider.GetRequiredService<IInAppNotificationService>();
        var profileDataService = scopedProvider.GetRequiredService<IProfileDataService>();
        var uiSettingsService = scopedProvider.GetRequiredService<IUISettingsService>();
        var tenantAccessor = scopedProvider.GetRequiredService<ITenantAccessor>();

        // Check if detection is enabled
        var settings = await uiSettingsService.GetSettingsAsync(cancellationToken);
        if (!settings.DataQuality.CompressionLowDetection.Enabled)
        {
            _logger.LogDebug("Compression low detection is disabled");
            return 0;
        }

        // Get sleep schedule from settings
        var sleepSchedule = settings.DataQuality.SleepSchedule;
        var bedtimeHour = sleepSchedule.BedtimeHour;
        var wakeTimeHour = sleepSchedule.WakeTimeHour;

        // Check if already processed (only active suggestions block re-detection)
        if (await repository.ActiveSuggestionsExistForNightAsync(nightOf, cancellationToken))
        {
            _logger.LogDebug("Already processed night of {NightOf}", nightOf);
            return 0;
        }

        // Get user's timezone: prefer UI settings, fall back to profile,
        // then infer from entry UTC offsets, then UTC
        var userTimeZone = ResolveTimeZone(sleepSchedule.Timezone)
            ?? await GetUserTimeZoneFromProfileAsync(profileDataService, nightOf, cancellationToken)
            ?? await InferTimeZoneFromEntriesAsync(entryService, nightOf, cancellationToken)
            ?? TimeZoneInfo.Utc;

        // Get overnight window in user's local time
        var (windowStart, windowEnd) = TimeZoneHelper.GetOvernightWindow(nightOf, userTimeZone, bedtimeHour, wakeTimeHour);

        // Get entries
        var entries = (await entryService.GetEntriesAsync(
            find: $"{{\"mills\":{{\"$gte\":{windowStart},\"$lte\":{windowEnd}}}}}",
            count: 1000,
            skip: 0,
            cancellationToken: cancellationToken))
            .Where(e => e.Sgv.HasValue)
            .OrderBy(e => e.Mills)
            .ToList();

        if (entries.Count < 10)
        {
            _logger.LogDebug("Insufficient entries for night of {NightOf}: {Count}", nightOf, entries.Count);
            return 0;
        }

        // Get treatments for IOB context
        var treatments = (await treatmentService.GetTreatmentsAsync(
            find: $"{{\"mills\":{{\"$gte\":{windowStart},\"$lte\":{windowEnd}}}}}",
            cancellationToken: cancellationToken))
            .ToList();

        // Detect V-shape candidates
        var candidates = DetectVShapeCandidates(entries);

        // Score candidates
        var suggestions = new List<CompressionLowSuggestion>();
        foreach (var candidate in candidates)
        {
            var confidence = ScoreCandidate(candidate, treatments, userTimeZone);
            if (confidence >= MinConfidenceThreshold)
            {
                var trimmedStartMills = candidate.StartMills + (StartTrimMinutes * 60 * 1000);
                suggestions.Add(new CompressionLowSuggestion
                {
                    Id = Guid.CreateVersion7(),
                    StartMills = trimmedStartMills,
                    EndMills = candidate.EndMills,
                    Confidence = confidence,
                    Status = CompressionLowStatus.Pending,
                    NightOf = nightOf,
                    CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    LowestGlucose = candidate.LowestGlucose,
                    DropRate = candidate.MaxDropRate,
                    RecoveryMinutes = candidate.RecoveryMinutes
                });
            }
        }

        // Save suggestions
        foreach (var suggestion in suggestions)
        {
            await repository.CreateAsync(suggestion, cancellationToken);
        }

        // Create notification if any suggestions found.
        // Notification uses i18n keys so the frontend can render localized text.
        // Notifications are keyed by subject ID (not tenant ID). Look up the tenant
        // owner so the notification is attributed to an actual user.
        if (suggestions.Count > 0)
        {
            var ownerId = await GetTenantOwnerSubjectIdAsync(
                tenantAccessor.TenantId, scopedProvider, cancellationToken);

            if (ownerId != null)
            {
                await CreateNotificationAsync(nightOf, suggestions.Count, ownerId, notificationService, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "No owner found for tenant {TenantId}; skipping compression low notification for night {NightOf}",
                    tenantAccessor.TenantId, nightOf);
            }
        }

        _logger.LogInformation(
            "Detected {Count} compression lows for night of {NightOf}",
            suggestions.Count, nightOf);

        return suggestions.Count;
    }

    internal record VShapeCandidate(
        long StartMills,
        long EndMills,
        double LowestGlucose,
        double PreDropGlucose,
        double MaxDropRate,
        int RecoveryMinutes,
        DateTime NadirTime);

    internal List<VShapeCandidate> DetectVShapeCandidates(List<Entry> entries)
    {
        var candidates = new List<VShapeCandidate>();

        for (int i = 2; i < entries.Count - 2; i++)
        {
            var current = entries[i];

            if (!current.Sgv.HasValue || current.Sgv.Value >= NadirThresholdMgDl)
                continue;

            var dropInfo = FindDrop(entries, i);
            if (dropInfo == null)
                continue;

            var recoveryInfo = FindRecovery(entries, i, dropInfo.Value.preDropValue);
            if (recoveryInfo == null)
                continue;

            candidates.Add(new VShapeCandidate(
                StartMills: entries[dropInfo.Value.startIndex].Mills,
                EndMills: entries[recoveryInfo.Value.endIndex].Mills,
                LowestGlucose: current.Sgv!.Value,
                PreDropGlucose: dropInfo.Value.preDropValue,
                MaxDropRate: dropInfo.Value.maxDropRate,
                RecoveryMinutes: recoveryInfo.Value.recoveryMinutes,
                NadirTime: current.Date ?? DateTime.UnixEpoch
            ));
        }

        return MergeOverlappingCandidates(candidates);
    }

    internal (int startIndex, double preDropValue, double maxDropRate)? FindDrop(
        List<Entry> entries, int nadirIndex)
    {
        var nadir = entries[nadirIndex];
        var maxDropRate = 0.0;
        var dropDurationMinutes = 0.0;
        int? earliestSteepIndex = null;

        for (int i = nadirIndex - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (!entry.Sgv.HasValue)
                continue;

            var timeDiffMinutes = (nadir.Mills - entry.Mills) / 60000.0;
            var glucoseDiff = entry.Sgv.Value - nadir.Sgv!.Value;
            var dropRate = glucoseDiff / timeDiffMinutes;

            if (dropRate > maxDropRate)
                maxDropRate = dropRate;

            if (dropRate >= MinDropRateMgDlPerMin)
            {
                dropDurationMinutes = timeDiffMinutes;
                earliestSteepIndex = i;
            }
            else if (dropDurationMinutes > 0)
            {
                if (dropDurationMinutes >= MinDropDurationMinutes)
                {
                    return (i + 1, entries[i + 1].Sgv!.Value, maxDropRate);
                }
                break;
            }
        }

        // The loop exhausted all entries while the drop rate stayed above the threshold.
        // This is the typical compression low pattern — steep drop all the way through.
        if (dropDurationMinutes >= MinDropDurationMinutes && earliestSteepIndex.HasValue)
        {
            return (earliestSteepIndex.Value, entries[earliestSteepIndex.Value].Sgv!.Value, maxDropRate);
        }

        return null;
    }

    internal (int endIndex, int recoveryMinutes)? FindRecovery(
        List<Entry> entries, int nadirIndex, double preDropValue)
    {
        var nadir = entries[nadirIndex];
        var targetValue = nadir.Sgv!.Value + (preDropValue - nadir.Sgv.Value) * (1 - RecoveryPercentage);

        for (int i = nadirIndex + 1; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (!entry.Sgv.HasValue)
                continue;

            var minutesSinceNadir = (entry.Mills - nadir.Mills) / 60000.0;

            if (minutesSinceNadir > MaxRecoveryMinutes)
                break;

            if (entry.Sgv.Value >= targetValue)
            {
                return (i, (int)minutesSinceNadir);
            }
        }

        return null;
    }

    internal List<VShapeCandidate> MergeOverlappingCandidates(List<VShapeCandidate> candidates)
    {
        if (candidates.Count <= 1)
            return candidates;

        var sorted = candidates.OrderBy(c => c.StartMills).ToList();
        var merged = new List<VShapeCandidate> { sorted[0] };

        foreach (var candidate in sorted.Skip(1))
        {
            var last = merged[^1];
            if (candidate.StartMills <= last.EndMills)
            {
                if (candidate.LowestGlucose < last.LowestGlucose)
                {
                    merged[^1] = candidate with
                    {
                        StartMills = last.StartMills,
                        EndMills = Math.Max(last.EndMills, candidate.EndMills)
                    };
                }
                else
                {
                    merged[^1] = last with
                    {
                        EndMills = Math.Max(last.EndMills, candidate.EndMills)
                    };
                }
            }
            else
            {
                merged.Add(candidate);
            }
        }

        return merged;
    }

    internal double ScoreCandidate(VShapeCandidate candidate, List<Treatment> treatments, TimeZoneInfo userTimeZone)
    {
        var score = 0.4; // Base score

        // V-shape clarity (0-0.3)
        var sharpness = candidate.MaxDropRate / 5.0;
        score += Math.Min(0.3, sharpness * 0.3);

        // Time of night (0-0.25)
        var nadirTimeLocal = TimeZoneInfo.ConvertTimeFromUtc(candidate.NadirTime, userTimeZone);
        var nadirHour = nadirTimeLocal.Hour;
        if (nadirHour >= 2 && nadirHour <= 5)
            score += 0.25;
        else if (nadirHour >= 0 && nadirHour <= 1)
            score += 0.15;
        else if (nadirHour == 23 || nadirHour == 6)
            score += 0.10;

        // Recovery completeness (0-0.2)
        var recoveryScore = 1.0 - (candidate.RecoveryMinutes / (double)MaxRecoveryMinutes);
        score += recoveryScore * 0.2;

        // IOB penalty (0-0.2 penalty)
        var iobPenalty = CalculateIobPenalty(candidate.StartMills, treatments);
        score -= iobPenalty;

        // Carb penalty (0-0.15 penalty)
        var carbPenalty = CalculateCarbPenalty(candidate.StartMills, treatments);
        score -= carbPenalty;

        return Math.Max(0, Math.Min(1, score));
    }

    private double CalculateIobPenalty(long dropStartMills, List<Treatment> treatments)
    {
        var windowStart = dropStartMills - (2 * 60 * 60 * 1000);
        var recentInsulin = treatments
            .Where(t => t.Mills >= windowStart && t.Mills <= dropStartMills)
            .Sum(t => t.Insulin ?? 0);

        return Math.Min(0.2, recentInsulin * 0.1);
    }

    private double CalculateCarbPenalty(long dropStartMills, List<Treatment> treatments)
    {
        var windowStart = dropStartMills - (2 * 60 * 60 * 1000);
        var recentCarbs = treatments
            .Where(t => t.Mills >= windowStart && t.Mills <= dropStartMills)
            .Sum(t => t.Carbs ?? 0);

        return Math.Min(0.15, recentCarbs / 200.0);
    }

    private async Task CreateNotificationAsync(
        DateOnly nightOf,
        int count,
        string userId,
        IInAppNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        // Use i18n keys for title/subtitle so the frontend renders localized text.
        // The metadata contains the count and nightOf for interpolation.
        await notificationService.CreateNotificationAsync(
            userId: userId,
            type: InAppNotificationType.CompressionLowReview,
            urgency: NotificationUrgency.Info,
            title: "compression_low_detected",
            subtitle: "compression_low_detected_subtitle",
            sourceId: nightOf.ToString("yyyy-MM-dd"),
            actions: new List<NotificationActionDto>
            {
                new()
                {
                    ActionId = "review",
                    Label = "review",
                    Icon = "eye",
                    Variant = "default"
                }
            },
            metadata: new Dictionary<string, object>
            {
                ["Count"] = count,
                ["NightOf"] = nightOf.ToString("yyyy-MM-dd")
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Resolves a timezone ID string to a TimeZoneInfo, returning null if the ID is empty or invalid.
    /// </summary>
    private static TimeZoneInfo? ResolveTimeZone(string? timezoneId)
    {
        if (string.IsNullOrEmpty(timezoneId))
            return null;

        var tz = TimeZoneHelper.GetTimeZoneInfoFromId(timezoneId);
        // GetTimeZoneInfoFromId returns UTC as fallback for unknown IDs;
        // only treat it as resolved if the input was explicitly "UTC"
        if (tz == TimeZoneInfo.Utc && !timezoneId.Equals("UTC", StringComparison.OrdinalIgnoreCase))
            return null;

        return tz;
    }

    /// <summary>
    /// Gets the user's timezone from the Nightscout profile active at the current time.
    /// Returns null if the profile has no timezone set.
    /// </summary>
    private async Task<TimeZoneInfo?> GetUserTimeZoneFromProfileAsync(
        IProfileDataService profileDataService,
        CancellationToken cancellationToken)
    {
        try
        {
            var profile = await profileDataService.GetProfileAtTimestampAsync(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), cancellationToken);
            var timezoneId = profile?.Store?.Values.FirstOrDefault()?.Timezone;
            return ResolveTimeZone(timezoneId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user timezone from profile");
            return null;
        }
    }

    /// <summary>
    /// Gets the user's timezone from the Nightscout profile active during a specific night.
    /// Returns null if the profile has no timezone set.
    /// </summary>
    private async Task<TimeZoneInfo?> GetUserTimeZoneFromProfileAsync(
        IProfileDataService profileDataService,
        DateOnly nightOf,
        CancellationToken cancellationToken)
    {
        try
        {
            var approximateNightTime = nightOf.ToDateTime(new TimeOnly(2, 0));
            var approximateMills = new DateTimeOffset(approximateNightTime, TimeSpan.Zero).ToUnixTimeMilliseconds();

            var profile = await profileDataService.GetProfileAtTimestampAsync(approximateMills, cancellationToken);
            var timezoneId = profile?.Store?.Values.FirstOrDefault()?.Timezone;
            return ResolveTimeZone(timezoneId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get user timezone from profile for night {NightOf}", nightOf);
            return null;
        }
    }

    /// <summary>
    /// Infers the user's timezone from the UtcOffset field on recent entries near the target night.
    /// Returns null if no entries with offset data are found.
    /// </summary>
    private async Task<TimeZoneInfo?> InferTimeZoneFromEntriesAsync(
        IEntryService entryService,
        DateOnly nightOf,
        CancellationToken cancellationToken)
    {
        try
        {
            // Query a small number of entries around midnight of the target night (in UTC)
            var midnightUtcMills = new DateTimeOffset(nightOf.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                .ToUnixTimeMilliseconds();
            var entries = await entryService.GetEntriesAsync(
                find: $"{{\"mills\":{{\"$gte\":{midnightUtcMills - 12 * 3600000},\"$lte\":{midnightUtcMills + 12 * 3600000}}}}}",
                count: 10,
                skip: 0,
                cancellationToken: cancellationToken);

            var utcOffset = entries
                .Where(e => e.UtcOffset.HasValue && e.UtcOffset.Value != 0)
                .Select(e => e.UtcOffset!.Value)
                .FirstOrDefault();

            if (utcOffset == 0)
                return null;

            var offset = TimeSpan.FromMinutes(utcOffset);
            var tz = TimeZoneInfo.CreateCustomTimeZone(
                $"Entry-UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours:D2}:{offset.Minutes:D2}",
                offset,
                $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours}:{offset.Minutes:D2}",
                $"UTC{(offset >= TimeSpan.Zero ? "+" : "")}{offset.Hours}:{offset.Minutes:D2}");

            _logger.LogInformation(
                "Inferred timezone UTC{Offset} from entry data for night {NightOf}",
                (offset >= TimeSpan.Zero ? "+" : "") + $"{offset.Hours}:{offset.Minutes:D2}", nightOf);

            return tz;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to infer timezone from entries for night {NightOf}", nightOf);
            return null;
        }
    }

    /// <summary>
    /// Looks up the subject ID of the tenant owner. Notifications require a real
    /// subject/user ID (not a tenant ID) so they can be queried by the authenticated
    /// user on the frontend.
    /// </summary>
    private async Task<string?> GetTenantOwnerSubjectIdAsync(
        Guid tenantId,
        IServiceProvider scopedProvider,
        CancellationToken cancellationToken)
    {
        var factory = scopedProvider.GetRequiredService<IDbContextFactory<NocturneDbContext>>();
        await using var context = await factory.CreateDbContextAsync(cancellationToken);

        var ownerSubjectId = await context.TenantMembers.AsNoTracking()
            .Where(tm => tm.TenantId == tenantId
                && tm.MemberRoles.Any(mr => mr.TenantRole.Slug == TenantPermissions.SeedRoles.Owner))
            .Select(tm => tm.SubjectId)
            .FirstOrDefaultAsync(cancellationToken);

        return ownerSubjectId == Guid.Empty ? null : ownerSubjectId.ToString();
    }
}

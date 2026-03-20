using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Nocturne.API.Helpers;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Services.ChartData.Stages;

/// <summary>
/// Pipeline stage that computes IOB/COB time series and the basal delivery series.
/// Extracts the heavy computation from ChartDataService: IOB/COB loop with caching,
/// SHA256 cache key generation, and basal step-function construction from TempBasal records.
/// </summary>
internal sealed class IobCobComputeStage(
    IIobService iobService,
    ICobService cobService,
    IProfileService profileService,
    IMemoryCache cache,
    ITenantAccessor tenantAccessor,
    ILogger<IobCobComputeStage> logger
) : IChartDataStage
{
    private static readonly TimeSpan IobCobCacheExpiration = TimeSpan.FromMinutes(1);

    private string TenantCacheId => tenantAccessor.Context?.TenantId.ToString()
        ?? throw new InvalidOperationException("Tenant context is not resolved");

    public Task<ChartDataContext> ExecuteAsync(ChartDataContext context, CancellationToken cancellationToken)
    {
        var syntheticTreatments = context.SyntheticTreatments.ToList();
        var deviceStatusList = context.DeviceStatusList.ToList();
        var tempBasalList = context.TempBasalList.ToList();
        var startTime = context.StartTime;
        var endTime = context.EndTime;
        var intervalMinutes = context.IntervalMinutes;
        var defaultBasalRate = context.DefaultBasalRate;

        var (iobSeries, cobSeries, maxIob, maxCob) = BuildIobCobSeries(
            syntheticTreatments,
            deviceStatusList,
            startTime,
            endTime,
            intervalMinutes,
            tempBasalList
        );

        var basalSeries = BuildBasalSeriesFromTempBasals(tempBasalList, startTime, endTime, defaultBasalRate);

        var maxBasalRate = Math.Max(
            defaultBasalRate * 2.5,
            basalSeries.Any() ? basalSeries.Max(b => b.Rate) : defaultBasalRate
        );

        return Task.FromResult(context with
        {
            IobSeries = iobSeries,
            CobSeries = cobSeries,
            MaxIob = Math.Max(3, maxIob),
            MaxCob = Math.Max(30, maxCob),
            BasalSeries = basalSeries,
            MaxBasalRate = maxBasalRate,
        });
    }

    internal (
        List<TimeSeriesPoint> iobSeries,
        List<TimeSeriesPoint> cobSeries,
        double maxIob,
        double maxCob
    ) BuildIobCobSeries(
        List<Treatment> treatments,
        List<DeviceStatus> deviceStatuses,
        long startTime,
        long endTime,
        int intervalMinutes,
        List<TempBasal>? tempBasals = null
    )
    {
        // Generate cache key based on treatment data hash and time range
        var cacheKey = GenerateIobCobCacheKey(treatments, startTime, endTime, intervalMinutes, tempBasals);

        // Try to get from cache
        if (
            cache.TryGetValue(
                cacheKey,
                out (
                    List<TimeSeriesPoint> iob,
                    List<TimeSeriesPoint> cob,
                    double maxIob,
                    double maxCob
                ) cached
            )
        )
        {
            logger.LogDebug("IOB/COB cache hit for range {Start}-{End}", startTime, endTime);
            return cached;
        }

        logger.LogDebug(
            "IOB/COB cache miss, computing for range {Start}-{End}",
            startTime,
            endTime
        );

        var iobSeries = new List<TimeSeriesPoint>();
        var cobSeries = new List<TimeSeriesPoint>();
        var intervalMs = intervalMinutes * 60 * 1000;
        double maxIob = 0,
            maxCob = 0;

        // Pre-compute DIA and COB absorption window for filtering
        var dia = profileService.HasData() ? profileService.GetDIA(endTime, null) : 3.0;
        var diaMs = (long)(dia * 60 * 60 * 1000); // DIA in milliseconds
        var cobAbsorptionMs = 6L * 60 * 60 * 1000; // 6 hours for COB absorption

        // Pre-filter treatments with insulin for IOB calculations
        var insulinTreatments = treatments
            .Where(t => t.Insulin.HasValue && t.Insulin.Value > 0)
            .ToList();

        // Pre-filter treatments with carbs for COB calculations
        var carbTreatments = treatments.Where(t => t.Carbs.HasValue && t.Carbs.Value > 0).ToList();

        var profile = profileService.HasData() ? profileService : null;

        for (long t = startTime; t <= endTime; t += intervalMs)
        {
            // Filter to only treatments that could still have active IOB at time t
            // A treatment can only contribute IOB if it was given within DIA hours before t
            var relevantIobTreatments = insulinTreatments
                .Where(tr => tr.Mills <= t && tr.Mills >= t - diaMs)
                .ToList();

            var iobResult =
                relevantIobTreatments.Count > 0
                    ? iobService.FromTreatments(relevantIobTreatments, profile, t, null)
                    : new IobResult { Iob = 0 };

            // Calculate basal IOB from V4 TempBasal records
            var basalIob = 0.0;
            if (tempBasals?.Count > 0)
            {
                var relevantTempBasals = tempBasals
                    .Where(tb => tb.StartMills <= t && tb.StartMills >= t - diaMs)
                    .ToList();

                if (relevantTempBasals.Count > 0)
                {
                    var basalResult = iobService.FromTempBasals(relevantTempBasals, profile, t, null);
                    basalIob = basalResult.BasalIob ?? 0;
                }
            }

            var iob = iobResult.Iob + basalIob;
            iobSeries.Add(new TimeSeriesPoint { Timestamp = t, Value = iob });
            if (iob > maxIob)
                maxIob = iob;

            // Filter to only treatments that could still have active COB at time t
            var relevantCobTreatments = carbTreatments
                .Where(tr => tr.Mills <= t && tr.Mills >= t - cobAbsorptionMs)
                .ToList();

            var cobResult =
                relevantCobTreatments.Count > 0
                    ? cobService.CobTotal(relevantCobTreatments, deviceStatuses, profile, t, null)
                    : new CobResult { Cob = 0 };

            var cob = cobResult.Cob;
            cobSeries.Add(new TimeSeriesPoint { Timestamp = t, Value = cob });
            if (cob > maxCob)
                maxCob = cob;
        }

        // Cache the result
        var result = (iobSeries, cobSeries, maxIob, maxCob);
        cache.Set(cacheKey, result, IobCobCacheExpiration);

        return result;
    }

    /// <summary>
    /// Generate a cache key for IOB/COB calculations based on treatment fingerprint and time range.
    /// Uses SHA256 of individual treatment mills/insulin/carbs values for collision resistance.
    /// Includes tenant ID to prevent cross-tenant cache leakage.
    /// </summary>
    private string GenerateIobCobCacheKey(
        List<Treatment> treatments,
        long startTime,
        long endTime,
        int intervalMinutes,
        List<TempBasal>? tempBasals = null
    )
    {
        // Round start/end times to interval boundaries for better cache hits
        var intervalMs = intervalMinutes * 60 * 1000;
        var roundedStart = (startTime / intervalMs) * intervalMs;
        var roundedEnd = (endTime / intervalMs) * intervalMs;

        // Hash individual treatment data for a collision-resistant fingerprint
        var sb = new StringBuilder();
        foreach (var t in treatments)
        {
            if (
                (t.Insulin.HasValue && t.Insulin.Value > 0)
                || (t.Carbs.HasValue && t.Carbs.Value > 0)
            )
            {
                sb.Append(t.Mills)
                    .Append(':')
                    .Append(t.Insulin ?? 0)
                    .Append(':')
                    .Append(t.Carbs ?? 0)
                    .Append('|');
            }
        }

        // Include temp basal data in cache key
        if (tempBasals != null)
        {
            foreach (var tb in tempBasals)
            {
                sb.Append(tb.StartMills)
                    .Append(':')
                    .Append(tb.Rate)
                    .Append(':')
                    .Append(tb.EndMills ?? 0)
                    .Append('|');
            }
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[
            ..16
        ]; // First 16 hex chars (64 bits) is sufficient

        return $"iobcob:{TenantCacheId}:{hash}:{roundedStart}:{roundedEnd}:{intervalMinutes}";
    }

    /// <summary>
    /// Build basal series from TempBasal records.
    /// TempBasal records are the v4 source of truth for pump-confirmed basal delivery.
    /// Falls back to profile-based rates when there are gaps in TempBasal data.
    /// </summary>
    internal List<BasalPoint> BuildBasalSeriesFromTempBasals(
        List<TempBasal> tempBasals,
        long startTime,
        long endTime,
        double defaultBasalRate
    )
    {
        var series = new List<BasalPoint>();
        var sorted = tempBasals.OrderBy(tb => tb.StartMills).ToList();

        logger.LogDebug(
            "Building basal series from {Count} TempBasal records",
            sorted.Count
        );

        if (sorted.Count == 0)
            return BuildBasalSeriesFromProfile(startTime, endTime, defaultBasalRate);

        long currentTime = startTime;

        foreach (var tb in sorted)
        {
            var tbStart = tb.StartMills;
            var tbEnd = tb.EndMills ?? endTime;

            if (tbEnd < startTime || tbStart > endTime)
                continue;

            tbStart = Math.Max(tbStart, startTime);
            tbEnd = Math.Min(tbEnd, endTime);

            if (tbStart > currentTime)
            {
                series.AddRange(
                    BuildBasalSeriesFromProfile(currentTime, tbStart, defaultBasalRate)
                );
            }

            var origin = MapTempBasalOrigin(tb.Origin);

            var scheduledRate = tb.ScheduledRate
                ?? (profileService.HasData()
                    ? profileService.GetBasalRate(tbStart, null)
                    : defaultBasalRate);

            series.Add(
                new BasalPoint
                {
                    Timestamp = tbStart,
                    Rate = origin == BasalDeliveryOrigin.Suspended ? 0 : tb.Rate,
                    ScheduledRate = scheduledRate,
                    Origin = origin,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(origin),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(origin),
                }
            );

            currentTime = tbEnd;
        }

        if (currentTime < endTime)
            series.AddRange(BuildBasalSeriesFromProfile(currentTime, endTime, defaultBasalRate));

        if (series.Count == 0)
        {
            series.Add(
                new BasalPoint
                {
                    Timestamp = startTime,
                    Rate = defaultBasalRate,
                    ScheduledRate = defaultBasalRate,
                    Origin = BasalDeliveryOrigin.Scheduled,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(BasalDeliveryOrigin.Scheduled),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                        BasalDeliveryOrigin.Scheduled
                    ),
                }
            );
        }

        return series;
    }

    internal List<BasalPoint> BuildBasalSeriesFromProfile(
        long startTime,
        long endTime,
        double defaultBasalRate
    )
    {
        var series = new List<BasalPoint>();
        const long intervalMs = 5 * 60 * 1000;
        double? prevRate = null;

        for (long t = startTime; t <= endTime; t += intervalMs)
        {
            var rate = profileService.HasData()
                ? profileService.GetBasalRate(t, null)
                : defaultBasalRate;

            if (prevRate == null || Math.Abs(rate - prevRate.Value) > 0.001)
            {
                series.Add(
                    new BasalPoint
                    {
                        Timestamp = t,
                        Rate = rate,
                        ScheduledRate = rate,
                        Origin = BasalDeliveryOrigin.Inferred,
                        FillColor = ChartColorMapper.FillFromBasalOrigin(
                            BasalDeliveryOrigin.Inferred
                        ),
                        StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                            BasalDeliveryOrigin.Inferred
                        ),
                    }
                );
                prevRate = rate;
            }
        }

        if (series.Count == 0)
        {
            series.Add(
                new BasalPoint
                {
                    Timestamp = startTime,
                    Rate = defaultBasalRate,
                    ScheduledRate = defaultBasalRate,
                    Origin = BasalDeliveryOrigin.Inferred,
                    FillColor = ChartColorMapper.FillFromBasalOrigin(BasalDeliveryOrigin.Inferred),
                    StrokeColor = ChartColorMapper.StrokeFromBasalOrigin(
                        BasalDeliveryOrigin.Inferred
                    ),
                }
            );
        }

        return series;
    }

    /// <summary>
    /// Maps a TempBasalOrigin enum value to the corresponding BasalDeliveryOrigin enum value.
    /// Both enums have identical members (Algorithm, Scheduled, Manual, Suspended, Inferred).
    /// </summary>
    internal static BasalDeliveryOrigin MapTempBasalOrigin(TempBasalOrigin origin) =>
        origin switch
        {
            TempBasalOrigin.Algorithm => BasalDeliveryOrigin.Algorithm,
            TempBasalOrigin.Scheduled => BasalDeliveryOrigin.Scheduled,
            TempBasalOrigin.Manual => BasalDeliveryOrigin.Manual,
            TempBasalOrigin.Suspended => BasalDeliveryOrigin.Suspended,
            TempBasalOrigin.Inferred => BasalDeliveryOrigin.Inferred,
            _ => BasalDeliveryOrigin.Scheduled,
        };
}

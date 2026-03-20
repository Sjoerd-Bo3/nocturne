using FluentAssertions;
using Nocturne.API.Services.AidDetection;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.API.Tests.Services;

public class AidMetricsServiceTests
{
    [Fact]
    public void AidSystemMetrics_ShouldHaveAllExpectedProperties()
    {
        var metrics = new AidSystemMetrics
        {
            CgmUsePercent = 95.0,
            PumpUsePercent = 88.0,
            AidActivePercent = 82.0,
            CgmActivePercent = 93.0,
            TargetLow = 70,
            TargetHigh = 180,
            SiteChangeCount = 4,
            Segments = []
        };

        metrics.CgmUsePercent.Should().Be(95.0);
        metrics.PumpUsePercent.Should().Be(88.0);
        metrics.AidActivePercent.Should().Be(82.0);
        metrics.CgmActivePercent.Should().Be(93.0);
        metrics.TargetLow.Should().Be(70);
        metrics.TargetHigh.Should().Be(180);
        metrics.SiteChangeCount.Should().Be(4);
        metrics.Segments.Should().BeEmpty();
    }

    [Fact]
    public void AidTimeSegment_ShouldTrackAlgorithmAndMetrics()
    {
        var segment = new AidTimeSegment
        {
            Algorithm = AidAlgorithm.Trio,
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            DurationHours = 12.0,
            Metrics = new AidSegmentMetrics
            {
                AidActivePercent = 95.0,
                PumpUsePercent = 100.0,
                LoopCycleCount = 144,
                EnactedCount = 137
            }
        };

        segment.Algorithm.Should().Be(AidAlgorithm.Trio);
        segment.DurationHours.Should().Be(12.0);
        segment.Metrics.AidActivePercent.Should().Be(95.0);
        segment.Metrics.EnactedCount.Should().Be(137);
    }

    [Fact]
    public void DeviceSegmentInput_ShouldMapDeviceToTimeWindow()
    {
        var input = new DeviceSegmentInput
        {
            Algorithm = AidAlgorithm.ControlIQ,
            StartDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(2024, 1, 7, 23, 59, 59, DateTimeKind.Utc),
        };

        input.Algorithm.Should().Be(AidAlgorithm.ControlIQ);
    }

    [Fact]
    public void ApsSnapshotStrategy_AllEnacted_Returns100Percent()
    {
        var strategy = new ApsSnapshotStrategy();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var snapshots = Enumerable.Range(0, 12).Select(i => new ApsSnapshot
        {
            Timestamp = start.AddMinutes(i * 5),
            Enacted = true,
            AidAlgorithm = AidAlgorithm.Trio
        }).ToList();

        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.Trio,
            StartDate = start,
            EndDate = end,
            ApsSnapshots = snapshots
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().Be(100.0);
        metrics.PumpUsePercent.Should().Be(100.0);
        metrics.LoopCycleCount.Should().Be(12);
        metrics.EnactedCount.Should().Be(12);
    }

    [Fact]
    public void ApsSnapshotStrategy_HalfEnacted_Returns50Percent()
    {
        var strategy = new ApsSnapshotStrategy();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var snapshots = Enumerable.Range(0, 12).Select(i => new ApsSnapshot
        {
            Timestamp = start.AddMinutes(i * 5),
            Enacted = i % 2 == 0,
            AidAlgorithm = AidAlgorithm.Loop
        }).ToList();

        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.Loop,
            StartDate = start,
            EndDate = end,
            ApsSnapshots = snapshots
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().Be(50.0);
        metrics.PumpUsePercent.Should().Be(100.0);
        metrics.LoopCycleCount.Should().Be(12);
        metrics.EnactedCount.Should().Be(6);
    }

    [Fact]
    public void ApsSnapshotStrategy_NoSnapshots_ReturnsNulls()
    {
        var strategy = new ApsSnapshotStrategy();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.Trio,
            StartDate = start,
            EndDate = end,
            ApsSnapshots = []
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().BeNull();
        metrics.PumpUsePercent.Should().BeNull();
        metrics.LoopCycleCount.Should().BeNull();
        metrics.EnactedCount.Should().BeNull();
    }

    [Fact]
    public void TbrBasedStrategy_AlgorithmTbrs_CalculatesPercent()
    {
        var strategy = new TbrBasedStrategy();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var tempBasals = new List<TempBasal>
        {
            new() { StartTimestamp = start, EndTimestamp = start.AddMinutes(30), Origin = TempBasalOrigin.Algorithm, Rate = 0.5 },
            new() { StartTimestamp = start.AddMinutes(30), EndTimestamp = end, Origin = TempBasalOrigin.Scheduled, Rate = 1.0 },
        };

        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.ControlIQ,
            StartDate = start,
            EndDate = end,
            TempBasals = tempBasals
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().Be(50.0);
        metrics.PumpUsePercent.Should().Be(100.0);
        metrics.LoopCycleCount.Should().BeNull();
        metrics.EnactedCount.Should().BeNull();
    }

    [Fact]
    public void TbrBasedStrategy_NoTempBasals_ReturnsNulls()
    {
        var strategy = new TbrBasedStrategy();
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 1, 1, 0, 0, DateTimeKind.Utc);

        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.ControlIQ,
            StartDate = start,
            EndDate = end,
            TempBasals = []
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().BeNull();
        metrics.PumpUsePercent.Should().BeNull();
    }

    [Fact]
    public void NoAidStrategy_AlwaysReturnsNulls()
    {
        var strategy = new NoAidStrategy();
        var context = new AidDetectionContext
        {
            Algorithm = AidAlgorithm.None,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddHours(1)
        };

        var metrics = strategy.CalculateMetrics(context);
        metrics.AidActivePercent.Should().BeNull();
        metrics.PumpUsePercent.Should().BeNull();
        metrics.LoopCycleCount.Should().BeNull();
        metrics.EnactedCount.Should().BeNull();
    }

    [Fact]
    public void ApsSnapshotStrategy_SupportsOpenSourceAlgorithms()
    {
        var strategy = new ApsSnapshotStrategy();
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.OpenAps);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.AndroidAps);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.Loop);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.Trio);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.IAPS);
    }

    [Fact]
    public void TbrBasedStrategy_SupportsCommercialAlgorithms()
    {
        var strategy = new TbrBasedStrategy();
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.ControlIQ);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.CamAPSFX);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.Omnipod5Algorithm);
        strategy.SupportedAlgorithms.Should().Contain(AidAlgorithm.MedtronicSmartGuard);
    }
}

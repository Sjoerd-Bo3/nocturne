using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Glooko.Models;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using V4BolusType = Nocturne.Core.Models.V4.BolusType;

namespace Nocturne.Connectors.Glooko.Mappers;

public class GlookoV4TreatmentMapper(string connectorSource, GlookoTimeMapper timeMapper, ILogger logger)
{
    private readonly string _connectorSource = connectorSource ?? throw new ArgumentNullException(nameof(connectorSource));
    private readonly GlookoTimeMapper _timeMapper = timeMapper ?? throw new ArgumentNullException(nameof(timeMapper));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Maps V2 batch data (NormalBoluses and Foods) to Bolus and CarbIntake records.
    /// </summary>
    public (List<Bolus> boluses, List<CarbIntake> carbs) MapBatchData(GlookoBatchData batchData)
    {
        var boluses = new List<Bolus>();
        var carbs = new List<CarbIntake>();

        try
        {
            if (batchData.NormalBoluses != null)
            {
                foreach (var bolus in batchData.NormalBoluses)
                {
                    try
                    {
                        var bolusDate = _timeMapper.GetRawGlookoDate(bolus.Timestamp, bolus.PumpTimestamp);
                        var correctedDate = _timeMapper.GetCorrectedGlookoTime(bolusDate);
                        var now = DateTime.UtcNow;

                        var hasInsulin = bolus.InsulinDelivered > 0;
                        var hasCarbs = bolus.CarbsInput > 0;

                        Guid? correlationId = hasInsulin && hasCarbs ? Guid.CreateVersion7() : null;

                        if (hasInsulin)
                        {
                            boluses.Add(new Bolus
                            {
                                Id = Guid.CreateVersion7(),
                                Timestamp = correctedDate,
                                LegacyId = GenerateLegacyId("bolus", bolusDate, $"insulin:{bolus.InsulinDelivered}_carbs:{bolus.CarbsInput}"),
                                Device = _connectorSource,
                                DataSource = _connectorSource,
                                Insulin = bolus.InsulinDelivered,
                                BolusType = V4BolusType.Normal,
                                Automatic = false,
                                CorrelationId = correlationId,
                                CreatedAt = now,
                                ModifiedAt = now
                            });
                        }

                        if (hasCarbs)
                        {
                            carbs.Add(new CarbIntake
                            {
                                Id = Guid.CreateVersion7(),
                                Timestamp = correctedDate,
                                LegacyId = GenerateLegacyId("carbs", bolusDate, $"insulin:{bolus.InsulinDelivered}_carbs:{bolus.CarbsInput}"),
                                Device = _connectorSource,
                                DataSource = _connectorSource,
                                Carbs = bolus.CarbsInput,
                                CorrelationId = correlationId,
                                CreatedAt = now,
                                ModifiedAt = now
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V2 bolus record", _connectorSource);
                    }
                }
            }

            if (batchData.Foods != null)
            {
                foreach (var food in batchData.Foods)
                {
                    try
                    {
                        var foodDate = _timeMapper.GetRawGlookoDate(food.Timestamp, food.PumpTimestamp);
                        var correctedDate = _timeMapper.GetCorrectedGlookoTime(foodDate);
                        var carbValue = food.Carbs > 0 ? food.Carbs : food.CarbohydrateGrams;

                        if (carbValue <= 0) continue;

                        var now = DateTime.UtcNow;
                        carbs.Add(new CarbIntake
                        {
                            Id = Guid.CreateVersion7(),
                            Timestamp = correctedDate,
                            LegacyId = GenerateLegacyId("food", foodDate, $"carbs:{carbValue}"),
                            Device = _connectorSource,
                            DataSource = _connectorSource,
                            Carbs = carbValue,
                            CreatedAt = now,
                            ModifiedAt = now
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V2 food record", _connectorSource);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ConnectorSource}] Error transforming Glooko V2 batch treatments", _connectorSource);
        }

        return (boluses, carbs);
    }

    /// <summary>
    /// Maps only standalone V2 Food records to CarbIntake records.
    /// Used when V3 handles boluses but V2 Foods have no V3 equivalent.
    /// </summary>
    public List<CarbIntake> MapFoods(GlookoBatchData batchData)
    {
        var carbs = new List<CarbIntake>();
        if (batchData.Foods == null) return carbs;

        foreach (var food in batchData.Foods)
        {
            try
            {
                var foodDate = _timeMapper.GetRawGlookoDate(food.Timestamp, food.PumpTimestamp);
                var correctedDate = _timeMapper.GetCorrectedGlookoTime(foodDate);
                var carbValue = food.Carbs > 0 ? food.Carbs : food.CarbohydrateGrams;

                if (carbValue <= 0) continue;

                var now = DateTime.UtcNow;
                carbs.Add(new CarbIntake
                {
                    Id = Guid.CreateVersion7(),
                    Timestamp = correctedDate,
                    LegacyId = GenerateLegacyId("food", foodDate, $"carbs:{carbValue}"),
                    Device = _connectorSource,
                    DataSource = _connectorSource,
                    Carbs = carbValue,
                    CreatedAt = now,
                    ModifiedAt = now
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V2 food record", _connectorSource);
            }
        }

        return carbs;
    }

    /// <summary>
    /// Maps V3 bolus series (DeliveredBolus, AutomaticBolus, InjectionBolus) to Bolus and CarbIntake records.
    /// </summary>
    public (List<Bolus> boluses, List<CarbIntake> carbs) MapV3Boluses(GlookoV3GraphResponse graphData)
    {
        var boluses = new List<Bolus>();
        var carbs = new List<CarbIntake>();

        if (graphData?.Series == null)
            return (boluses, carbs);

        var series = graphData.Series;

        var deliveredBoluses = (series.DeliveredBolus ?? []).Select(b => (bolus: b, automatic: false));
        var automaticBoluses = (series.AutomaticBolus ?? []).Select(b => (bolus: b, automatic: true));
        var injectionBoluses = (series.InjectionBolus ?? []).Select(b => (bolus: b, automatic: false));

        var allBolusSeries = deliveredBoluses.Concat(automaticBoluses).Concat(injectionBoluses);

        foreach (var (bolus, automatic) in allBolusSeries)
        {
            try
            {
                var rawTimestamp = DateTimeOffset.FromUnixTimeSeconds(bolus.X).UtcDateTime;
                var correctedTimestamp = _timeMapper.GetCorrectedGlookoTime(bolus.X);
                var now = DateTime.UtcNow;

                var insulin = bolus.Data?.DeliveredUnits ?? bolus.Data?.ProgrammedUnits ?? bolus.Y;
                var carbsInput = bolus.Data?.CarbsInput ?? 0;

                var hasInsulin = insulin > 0;
                var hasCarbs = carbsInput > 0;

                Guid? correlationId = hasInsulin && hasCarbs ? Guid.CreateVersion7() : null;

                if (hasInsulin)
                {
                    boluses.Add(new Bolus
                    {
                        Id = Guid.CreateVersion7(),
                        Timestamp = correctedTimestamp,
                        LegacyId = GenerateLegacyId("v3_bolus", rawTimestamp, $"carbs:{carbsInput}_insulin:{insulin}"),
                        Device = _connectorSource,
                        DataSource = _connectorSource,
                        Insulin = insulin,
                        BolusType = V4BolusType.Normal,
                        Automatic = automatic,
                        CorrelationId = correlationId,
                        CreatedAt = now,
                        ModifiedAt = now
                    });
                }

                if (hasCarbs)
                {
                    carbs.Add(new CarbIntake
                    {
                        Id = Guid.CreateVersion7(),
                        Timestamp = correctedTimestamp,
                        LegacyId = GenerateLegacyId("v3_carbs", rawTimestamp, $"carbs:{carbsInput}_insulin:{insulin}"),
                        Device = _connectorSource,
                        DataSource = _connectorSource,
                        Carbs = carbsInput,
                        CorrelationId = correlationId,
                        CreatedAt = now,
                        ModifiedAt = now
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V3 bolus record at X={X}", _connectorSource, bolus.X);
            }
        }

        _logger.LogInformation(
            "[{ConnectorSource}] Transformed {BolusCount} boluses and {CarbCount} carb intakes from v3 data",
            _connectorSource, boluses.Count, carbs.Count);

        return (boluses, carbs);
    }

    /// <summary>
    /// Maps V3 consumable change series (ReservoirChange, SetSiteChange) to DeviceEvent records.
    /// PumpAlarms are skipped here — they are handled by GlookoSystemEventMapper.
    /// </summary>
    public List<DeviceEvent> MapV3DeviceEvents(GlookoV3GraphResponse graphData)
    {
        var deviceEvents = new List<DeviceEvent>();

        if (graphData?.Series == null)
            return deviceEvents;

        var series = graphData.Series;

        if (series.ReservoirChange != null)
        {
            foreach (var change in series.ReservoirChange)
            {
                try
                {
                    var rawTimestamp = DateTimeOffset.FromUnixTimeSeconds(change.X).UtcDateTime;
                    var correctedTimestamp = _timeMapper.GetCorrectedGlookoTime(change.X);
                    var now = DateTime.UtcNow;

                    deviceEvents.Add(new DeviceEvent
                    {
                        Id = Guid.CreateVersion7(),
                        Timestamp = correctedTimestamp,
                        LegacyId = GenerateLegacyId("reservoir_change", rawTimestamp),
                        Device = _connectorSource,
                        DataSource = _connectorSource,
                        EventType = DeviceEventType.ReservoirChange,
                        Notes = change.Label,
                        CreatedAt = now,
                        ModifiedAt = now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V3 reservoir change at X={X}", _connectorSource, change.X);
                }
            }
        }

        if (series.SetSiteChange != null)
        {
            foreach (var change in series.SetSiteChange)
            {
                try
                {
                    var rawTimestamp = DateTimeOffset.FromUnixTimeSeconds(change.X).UtcDateTime;
                    var correctedTimestamp = _timeMapper.GetCorrectedGlookoTime(change.X);
                    var now = DateTime.UtcNow;

                    deviceEvents.Add(new DeviceEvent
                    {
                        Id = Guid.CreateVersion7(),
                        Timestamp = correctedTimestamp,
                        LegacyId = GenerateLegacyId("site_change", rawTimestamp),
                        Device = _connectorSource,
                        DataSource = _connectorSource,
                        EventType = DeviceEventType.SiteChange,
                        Notes = change.Label,
                        CreatedAt = now,
                        ModifiedAt = now
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[{ConnectorSource}] Error mapping V3 site change at X={X}", _connectorSource, change.X);
                }
            }
        }

        _logger.LogInformation(
            "[{ConnectorSource}] Transformed {Count} device events from v3 data",
            _connectorSource, deviceEvents.Count);

        return deviceEvents;
    }

    private static string GenerateLegacyId(string eventType, DateTime timestamp, string? additionalData = null)
    {
        var dataToHash = $"glooko_{eventType}_{timestamp.Ticks}_{additionalData ?? string.Empty}";
        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
        return $"glooko_{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }
}

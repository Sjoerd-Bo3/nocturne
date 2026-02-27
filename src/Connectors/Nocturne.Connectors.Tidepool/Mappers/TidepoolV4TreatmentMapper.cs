using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Tidepool.Models;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Tidepool.Mappers;

public class TidepoolV4TreatmentMapper(ILogger logger, string connectorSource)
{
    private readonly string _connectorSource = connectorSource ?? throw new ArgumentNullException(nameof(connectorSource));
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public (List<Bolus> Boluses, List<CarbIntake> CarbIntakes) MapTreatments(
        TidepoolBolus[]? boluses,
        TidepoolFood[]? foods)
    {
        var mappedBoluses = new Dictionary<DateTime, Bolus>();
        var mappedCarbs = new List<CarbIntake>();

        // Process boluses first
        if (boluses != null)
        {
            foreach (var bolus in boluses.Where(b => b.Time.HasValue))
            {
                try
                {
                    var mapped = MapBolus(bolus);
                    if (mapped != null)
                        mappedBoluses[bolus.Time!.Value] = mapped;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping Tidepool bolus: {Id}", bolus.Id);
                }
            }
        }

        // Process foods: correlate with bolus at same timestamp if present
        if (foods != null)
        {
            foreach (var food in foods.Where(f => f.Time.HasValue))
            {
                try
                {
                    var carbs = food.Nutrition?.Carbohydrate?.Net;
                    if (!carbs.HasValue || carbs.Value <= 0) continue;

                    var timestamp = food.Time!.Value.ToUniversalTime();
                    var now = DateTime.UtcNow;

                    if (mappedBoluses.TryGetValue(food.Time!.Value, out var existingBolus))
                    {
                        // Correlate bolus and carb at the same timestamp
                        var correlationId = Guid.CreateVersion7();
                        existingBolus.CorrelationId = correlationId;

                        mappedCarbs.Add(new CarbIntake
                        {
                            Id = Guid.CreateVersion7(),
                            Timestamp = timestamp,
                            LegacyId = $"tidepool_{food.Id}",
                            Device = _connectorSource,
                            DataSource = _connectorSource,
                            Carbs = carbs.Value,
                            CorrelationId = correlationId,
                            CreatedAt = now,
                            ModifiedAt = now
                        });
                    }
                    else
                    {
                        // Standalone carb entry
                        mappedCarbs.Add(new CarbIntake
                        {
                            Id = Guid.CreateVersion7(),
                            Timestamp = timestamp,
                            LegacyId = $"tidepool_{food.Id}",
                            Device = _connectorSource,
                            DataSource = _connectorSource,
                            Carbs = carbs.Value,
                            CreatedAt = now,
                            ModifiedAt = now
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error mapping Tidepool food: {Id}", food.Id);
                }
            }
        }

        return (mappedBoluses.Values.ToList(), mappedCarbs);
    }

    private Bolus? MapBolus(TidepoolBolus bolus)
    {
        if (!bolus.Time.HasValue) return null;

        var totalInsulin = (bolus.Normal ?? 0) + (bolus.Extended ?? 0);
        if (totalInsulin <= 0) return null;

        var timestamp = bolus.Time!.Value.ToUniversalTime();
        var now = DateTime.UtcNow;

        BolusType bolusType;
        if (string.Equals(bolus.SubType, "dual/square", StringComparison.OrdinalIgnoreCase)
            || (bolus.Normal.HasValue && bolus.Normal > 0 && bolus.Extended.HasValue && bolus.Extended > 0))
        {
            bolusType = BolusType.Dual;
        }
        else if (string.Equals(bolus.SubType, "square", StringComparison.OrdinalIgnoreCase)
            || (bolus.Extended.HasValue && bolus.Extended > 0))
        {
            bolusType = BolusType.Square;
        }
        else
        {
            bolusType = BolusType.Normal;
        }

        double? durationMinutes = bolus.Duration.HasValue
            ? bolus.Duration.Value.TotalMinutes
            : null;

        return new Bolus
        {
            Id = Guid.CreateVersion7(),
            Timestamp = timestamp,
            LegacyId = $"tidepool_{bolus.Id}",
            Device = _connectorSource,
            DataSource = _connectorSource,
            Insulin = totalInsulin,
            BolusType = bolusType,
            Automatic = false,
            Duration = durationMinutes,
            CreatedAt = now,
            ModifiedAt = now
        };
    }
}

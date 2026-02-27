using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Dexcom.Models;
using Nocturne.Connectors.Dexcom.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.V4;

namespace Nocturne.Connectors.Dexcom.Mappers;

public class DexcomSensorGlucoseMapper(ILogger logger)
{
    private static readonly Dictionary<int, GlucoseDirection> TrendDirections = new()
    {
        { 0, GlucoseDirection.None },
        { 1, GlucoseDirection.DoubleUp },
        { 2, GlucoseDirection.SingleUp },
        { 3, GlucoseDirection.FortyFiveUp },
        { 4, GlucoseDirection.Flat },
        { 5, GlucoseDirection.FortyFiveDown },
        { 6, GlucoseDirection.SingleDown },
        { 7, GlucoseDirection.DoubleDown },
        { 8, GlucoseDirection.NotComputable },
        { 9, GlucoseDirection.RateOutOfRange }
    };

    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public IEnumerable<SensorGlucose> MapBatchData(DexcomEntry[]? batchData)
    {
        if (batchData == null || batchData.Length == 0) return [];

        var now = DateTime.UtcNow;
        return batchData
            .Where(entry => entry.Value > 0)
            .Select(entry => ConvertEntry(entry, now))
            .Where(sg => sg != null)
            .Cast<SensorGlucose>()
            .OrderBy(sg => sg.Mills)
            .ToList();
    }

    private SensorGlucose? ConvertEntry(DexcomEntry dexcomEntry, DateTime now)
    {
        try
        {
            if (!DexcomTimestampParser.TryParse(dexcomEntry.Wt, out var timestamp))
            {
                _logger.LogWarning("Could not parse Dexcom timestamp: {Timestamp}", dexcomEntry.Wt);
                return null;
            }

            var direction = TrendDirections.GetValueOrDefault(dexcomEntry.Trend, GlucoseDirection.NotComputable);
            var mgdl = (double)dexcomEntry.Value;

            return new SensorGlucose
            {
                Id = Guid.CreateVersion7(),
                Timestamp = timestamp,
                LegacyId = $"dexcom_{dexcomEntry.Wt}",
                Device = DataSources.DexcomConnector,
                DataSource = DataSources.DexcomConnector,
                Mgdl = mgdl,
                Direction = direction,
                CreatedAt = now,
                ModifiedAt = now
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error converting Dexcom entry: {@Entry}", dexcomEntry);
            return null;
        }
    }
}

using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Represents the main data structure used by Nightscout clients.
/// Contains all necessary data for glucose monitoring and treatment tracking.
/// This class provides 1:1 backwards compatibility with the legacy JavaScript DData implementation.
/// </summary>
/// <seealso cref="Entry"/>
/// <seealso cref="Treatment"/>
/// <seealso cref="DeviceStatus"/>
/// <seealso cref="Profile"/>
/// <seealso cref="Food"/>
/// <seealso cref="Activity"/>
/// <seealso cref="DDataResponse"/>
public class DData
{
    /// <summary>
    /// Gets or sets the glucose entries (SGV - Sensor Glucose Values)
    /// </summary>
    [JsonPropertyName("sgvs")]
    public List<Entry> Sgvs { get; set; } = new();

    /// <summary>
    /// Gets or sets the treatment records
    /// </summary>
    [JsonPropertyName("treatments")]
    public List<Treatment> Treatments { get; set; } = new();

    /// <summary>
    /// Gets or sets the meter blood glucose readings
    /// </summary>
    [JsonPropertyName("mbgs")]
    public List<Entry> Mbgs { get; set; } = new();

    /// <summary>
    /// Gets or sets the calibration records
    /// </summary>
    [JsonPropertyName("cals")]
    public List<Entry> Cals { get; set; } = new();

    /// <summary>
    /// Gets or sets the most recent calibration record
    /// </summary>
    [JsonPropertyName("cal")]
    public Entry? Cal { get; set; }

    /// <summary>
    /// Gets or sets the profile records
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the device status records
    /// </summary>
    [JsonPropertyName("devicestatus")]
    public List<DeviceStatus> DeviceStatus { get; set; } = new();

    /// <summary>
    /// Gets or sets the food database entries
    /// </summary>
    [JsonPropertyName("food")]
    public List<Food> Food { get; set; } = new();

    /// <summary>
    /// Gets or sets the activity records
    /// </summary>
    [JsonPropertyName("activity")]
    public List<Activity> Activity { get; set; } = new();

    /// <summary>
    /// Gets or sets database statistics
    /// </summary>
    [JsonPropertyName("dbstats")]
    public DbStats DbStats { get; set; } = new();

    /// <summary>
    /// Gets or sets the timestamp when this data was last updated (Unix milliseconds)
    /// </summary>
    [JsonPropertyName("lastUpdated")]
    public long LastUpdated { get; set; }

    /// <summary>
    /// Gets or sets whether the client is in retro mode (viewing historical data)
    /// </summary>
    [JsonPropertyName("inRetroMode")]
    public bool? InRetroMode { get; set; }

    /// <summary>
    /// Gets or sets site change treatments (filtered from treatments)
    /// </summary>
    [JsonPropertyName("sitechangeTreatments")]
    public List<Treatment> SiteChangeTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets insulin change treatments (filtered from treatments)
    /// </summary>
    [JsonPropertyName("insulinchangeTreatments")]
    public List<Treatment> InsulinChangeTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets battery change treatments (filtered from treatments)
    /// </summary>
    [JsonPropertyName("batteryTreatments")]
    public List<Treatment> BatteryTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets sensor treatments (filtered from treatments)
    /// </summary>
    [JsonPropertyName("sensorTreatments")]
    public List<Treatment> SensorTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets combo bolus treatments (filtered from treatments)
    /// </summary>
    [JsonPropertyName("combobolusTreatments")]
    public List<Treatment> ComboBolusTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets profile switch treatments (filtered and processed from treatments)
    /// </summary>
    [JsonPropertyName("profileTreatments")]
    public List<Treatment> ProfileTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets temporary basal treatments (filtered and processed from treatments)
    /// </summary>
    [JsonPropertyName("tempbasalTreatments")]
    public List<Treatment> TempBasalTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets temporary target treatments (filtered and processed from treatments)
    /// </summary>
    [JsonPropertyName("tempTargetTreatments")]
    public List<Treatment> TempTargetTreatments { get; set; } = new();

    /// <summary>
    /// Gets or sets the profile name from the most recent zero-duration Profile Switch treatment.
    /// Used by Loop and other clients for active profile determination.
    /// </summary>
    [JsonPropertyName("lastProfileFromSwitch")]
    public string? LastProfileFromSwitch { get; set; }
}

/// <summary>
/// Represents database statistics
/// </summary>
public class DbStats
{
    /// <summary>
    /// Gets or sets the total data size
    /// </summary>
    [JsonPropertyName("dataSize")]
    public long DataSize { get; set; }

    /// <summary>
    /// Gets or sets the number of collections
    /// </summary>
    [JsonPropertyName("collections")]
    public int Collections { get; set; }

    /// <summary>
    /// Gets or sets the number of indexes
    /// </summary>
    [JsonPropertyName("indexes")]
    public int Indexes { get; set; }
}

/// <summary>
/// Response structure for DData endpoints that includes recent device status
/// This matches the dataWithRecentStatuses method from the legacy implementation
/// </summary>
public class DDataResponse
{
    /// <summary>
    /// Gets or sets the recent device status entries
    /// </summary>
    [JsonPropertyName("devicestatus")]
    public List<DeviceStatus> DeviceStatus { get; set; } = new();

    /// <summary>
    /// Gets or sets the glucose entries
    /// </summary>
    [JsonPropertyName("sgvs")]
    public List<Entry> Sgvs { get; set; } = new();

    /// <summary>
    /// Gets or sets the calibration records
    /// </summary>
    [JsonPropertyName("cals")]
    public List<Entry> Cals { get; set; } = new();

    /// <summary>
    /// Gets or sets the profile records (with temporary profile switches filtered out)
    /// </summary>
    [JsonPropertyName("profiles")]
    public List<Profile> Profiles { get; set; } = new();

    /// <summary>
    /// Gets or sets the meter blood glucose readings
    /// </summary>
    [JsonPropertyName("mbgs")]
    public List<Entry> Mbgs { get; set; } = new();

    /// <summary>
    /// Gets or sets the food database entries
    /// </summary>
    [JsonPropertyName("food")]
    public List<Food> Food { get; set; } = new();

    /// <summary>
    /// Gets or sets the treatment records
    /// </summary>
    [JsonPropertyName("treatments")]
    public List<Treatment> Treatments { get; set; } = new();

    /// <summary>
    /// Gets or sets database statistics
    /// </summary>
    [JsonPropertyName("dbstats")]
    public DbStats DbStats { get; set; } = new();
}

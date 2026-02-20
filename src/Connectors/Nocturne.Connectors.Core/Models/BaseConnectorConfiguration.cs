using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.Connectors.Core.Models;

/// <summary>
///     Base implementation of connector configuration with common properties
/// </summary>
public abstract class BaseConnectorConfiguration : IConnectorConfiguration
{
    /// <summary>
    ///     Gets the connector name from the ConnectorRegistration attribute.
    ///     Used for error messages and logging.
    /// </summary>
    private string ConnectorName =>
        GetType().GetCustomAttribute<ConnectorRegistrationAttribute>()?.ConnectorName
        ?? GetType().Name.Replace("Configuration", "");

    /// <summary>
    ///     Gets the environment variable prefix from the ConnectorRegistration attribute.
    /// </summary>
    private string? EnvPrefix =>
        GetType().GetCustomAttribute<ConnectorRegistrationAttribute>()?.EnvironmentPrefix;
    /// <summary>
    ///     Timezone offset in hours (default 0).
    ///     Can be set via environment variable: CONNECT_{CONNECTORNAME}_TIMEZONE_OFFSET
    ///     or appsettings: {Configuration}:TimezoneOffset
    /// </summary>
    [ConnectorProperty("TimezoneOffset",
        RuntimeConfigurable = true,
        DisplayName = "Timezone Offset",
        Category = "General",
        MinValue = -12,
        MaxValue = 14)]
    public double TimezoneOffset { get; set; } = 0;

    [Required] public ConnectSource ConnectSource { get; set; }

    /// <summary>
    ///     Whether the connector is enabled and should sync data.
    ///     When disabled, the connector enters standby mode.
    /// </summary>
    [ConnectorProperty("Enabled",
        RuntimeConfigurable = true,
        DisplayName = "Enabled",
        Category = "General")]
    public bool Enabled { get; set; } = true;

    [ConnectorProperty("MaxRetryAttempts",
        RuntimeConfigurable = true,
        DisplayName = "Max Retry Attempts",
        Category = "Advanced",
        MinValue = 0,
        MaxValue = 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    [ConnectorProperty("BatchSize",
        RuntimeConfigurable = true,
        DisplayName = "Batch Size",
        Category = "Advanced",
        MinValue = 1,
        MaxValue = 500)]
    public int BatchSize { get; set; } = 50;

    [ConnectorProperty("SyncIntervalMinutes",
        RuntimeConfigurable = true,
        DisplayName = "Sync Interval (Minutes)",
        Category = "Sync",
        MinValue = 1,
        MaxValue = 60)]
    public int SyncIntervalMinutes { get; set; } = 5;

    [ConnectorProperty("SyncGlucose", RuntimeConfigurable = true, DisplayName = "Sync Glucose", Category = "Sync",
        Description = "Sync CGM glucose data", DefaultValue = "true")]
    public bool SyncGlucose { get; set; } = true;

    [ConnectorProperty("SyncManualBG", RuntimeConfigurable = true, DisplayName = "Sync Manual BG", Category = "Sync",
        Description = "Sync manual finger prick blood glucose checks", DefaultValue = "true")]
    public bool SyncManualBG { get; set; } = true;

    [ConnectorProperty("SyncBoluses", RuntimeConfigurable = true, DisplayName = "Sync Boluses", Category = "Sync",
        Description = "Sync insulin bolus data", DefaultValue = "true")]
    public bool SyncBoluses { get; set; } = true;

    [ConnectorProperty("SyncCarbIntake", RuntimeConfigurable = true, DisplayName = "Sync Carb Intake", Category = "Sync",
        Description = "Sync carbohydrate intake data", DefaultValue = "true")]
    public bool SyncCarbIntake { get; set; } = true;

    [ConnectorProperty("SyncBolusCalculations", RuntimeConfigurable = true, DisplayName = "Sync Bolus Calculations", Category = "Sync",
        Description = "Sync bolus calculator/wizard data", DefaultValue = "true")]
    public bool SyncBolusCalculations { get; set; } = true;

    [ConnectorProperty("SyncNotes", RuntimeConfigurable = true, DisplayName = "Sync Notes", Category = "Sync",
        Description = "Sync notes and annotations", DefaultValue = "true")]
    public bool SyncNotes { get; set; } = true;

    [ConnectorProperty("SyncDeviceEvents", RuntimeConfigurable = true, DisplayName = "Sync Device Events", Category = "Sync",
        Description = "Sync device events (site changes, sensor starts, etc.)", DefaultValue = "true")]
    public bool SyncDeviceEvents { get; set; } = true;

    [ConnectorProperty("SyncStateSpans", RuntimeConfigurable = true, DisplayName = "Sync State Spans", Category = "Sync",
        Description = "Sync basal delivery and pump mode state spans", DefaultValue = "true")]
    public bool SyncStateSpans { get; set; } = true;

    [ConnectorProperty("SyncProfiles", RuntimeConfigurable = true, DisplayName = "Sync Profiles", Category = "Sync",
        Description = "Sync profile data (basal rates, ISF, carb ratio)", DefaultValue = "true")]
    public bool SyncProfiles { get; set; } = true;

    [ConnectorProperty("SyncDeviceStatus", RuntimeConfigurable = true, DisplayName = "Sync Device Status", Category = "Sync",
        Description = "Sync device status data", DefaultValue = "true")]
    public bool SyncDeviceStatus { get; set; } = true;

    [ConnectorProperty("SyncActivity", RuntimeConfigurable = true, DisplayName = "Sync Activity", Category = "Sync",
        Description = "Sync exercise and activity data", DefaultValue = "true")]
    public bool SyncActivity { get; set; } = true;

    [ConnectorProperty("SyncFood", RuntimeConfigurable = true, DisplayName = "Sync Food", Category = "Sync",
        Description = "Sync food diary data", DefaultValue = "true")]
    public bool SyncFood { get; set; } = true;

    public bool IsDataTypeEnabled(SyncDataType type) => type switch
    {
        SyncDataType.Glucose => SyncGlucose,
        SyncDataType.ManualBG => SyncManualBG,
        SyncDataType.Boluses => SyncBoluses,
        SyncDataType.CarbIntake => SyncCarbIntake,
        SyncDataType.BolusCalculations => SyncBolusCalculations,
        SyncDataType.Notes => SyncNotes,
        SyncDataType.DeviceEvents => SyncDeviceEvents,
        SyncDataType.StateSpans => SyncStateSpans,
        SyncDataType.Profiles => SyncProfiles,
        SyncDataType.DeviceStatus => SyncDeviceStatus,
        SyncDataType.Activity => SyncActivity,
        SyncDataType.Food => SyncFood,
        _ => true
    };

    public List<SyncDataType> GetEnabledDataTypes(List<SyncDataType> supportedTypes)
        => supportedTypes.Where(IsDataTypeEnabled).ToList();

    public virtual void Validate()
    {
        if (!Enum.IsDefined(typeof(ConnectSource), ConnectSource))
            throw new ArgumentException($"Invalid connector source: {ConnectSource}");

        if (MaxRetryAttempts < 0)
            throw new ArgumentException("MaxRetryAttempts cannot be negative");

        if (BatchSize <= 0)
            throw new ArgumentException("BatchSize must be greater than zero");

        // Validate properties marked with [Required] or [ConnectorProperty(Required = true)]
        ValidateRequiredProperties();

        // Allow derived classes to add additional validation
        ValidateSourceSpecificConfiguration();
    }

    /// <summary>
    ///     Validates all properties marked with [Required] attribute or
    ///     [ConnectorProperty(Required = true)].
    ///     Throws ArgumentException if any required string property is null or empty.
    /// </summary>
    private void ValidateRequiredProperties()
    {
        var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            var isRequired = false;
            string? displayName = null;

            // Check for [Required] attribute
            if (property.GetCustomAttribute<RequiredAttribute>() != null)
            {
                isRequired = true;
                displayName = property.Name;
            }

            // Check for [ConnectorProperty(Required = true)]
            var connectorProp = property.GetCustomAttribute<ConnectorPropertyAttribute>();
            if (connectorProp is { Required: true })
            {
                isRequired = true;
                displayName = connectorProp.GetDisplayName();
            }

            if (!isRequired)
                continue;

            var value = property.GetValue(this);

            // For string properties, check for null or whitespace
            if (property.PropertyType == typeof(string))
            {
                if (!string.IsNullOrWhiteSpace(value as string)) continue;
                var envVarHint = connectorProp != null && EnvPrefix != null
                    ? $" (set via {connectorProp.GetFullEnvVarName(EnvPrefix)} or configuration)"
                    : "";
                throw new ArgumentException(
                    $"{ConnectorName}: {displayName} is required{envVarHint}");
            }
            // For nullable value types, check for null
            else if (Nullable.GetUnderlyingType(property.PropertyType) != null && value == null)
            {
                throw new ArgumentException(
                    $"{ConnectorName}: {displayName} is required");
            }
        }
    }

    /// <summary>
    ///     Override this method to add connector-specific validation beyond [Required] properties.
    ///     The base implementation does nothing - derived classes can add custom validation rules.
    /// </summary>
    protected virtual void ValidateSourceSpecificConfiguration()
    {
        // Default implementation: no additional validation needed
        // Derived classes can override to add custom validation
    }
}
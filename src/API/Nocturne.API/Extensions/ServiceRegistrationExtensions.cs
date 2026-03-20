using Nocturne.API.Configuration;
using Nocturne.API.Middleware.Handlers;
using Nocturne.API.Services;
using Nocturne.API.Services.AidDetection;
using Nocturne.API.Services.Alerts;
using Nocturne.API.Services.Alerts.Notifiers;
using Nocturne.API.Services.Alerts.Webhooks;
using Nocturne.API.Services.Auth;
using Nocturne.API.Services.BackgroundServices;
using Nocturne.API.Services.ConnectorPublishing;
using Nocturne.API.Services.V4;
using Nocturne.API.Multitenancy;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Alerts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Infrastructure.Data.Repositories.V4;
using Nocturne.Infrastructure.Data.Services;
using Nocturne.Infrastructure.Shared.Services;
using EmailOptions = Nocturne.Core.Models.Configuration.EmailOptions;
using JwtOptions = Nocturne.Core.Models.Configuration.JwtOptions;
using LocalIdentityOptions = Nocturne.Core.Models.Configuration.LocalIdentityOptions;
using OidcOptions = Nocturne.Core.Models.Configuration.OidcOptions;

namespace Nocturne.API.Extensions;

/// <summary>
/// Extension methods that organize DI registrations into logical groups,
/// keeping Program.cs scannable.
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Core API utility and calculation services (status, versioning, time queries,
    /// IOB/COB, predictions, statistics, etc.)
    /// </summary>
    public static IServiceCollection AddApiCoreServices(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<IStatusService, StatusService>();
        services.AddScoped<IVersionService, VersionService>();
        services.AddSingleton<IXmlDocumentationService, XmlDocumentationService>();
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        services.AddScoped<ITreatmentProcessingService, TreatmentProcessingService>();

        services.AddScoped<IBraceExpansionService, BraceExpansionService>();
        services.AddScoped<ITimeQueryService, TimeQueryService>();

        services.AddScoped<IDDataService, DDataService>();
        services.AddScoped<IPropertiesService, PropertiesService>();
        services.AddScoped<ISummaryService, SummaryService>();
        services.AddScoped<IIobService, IobService>();

        // Prediction service — configurable via Predictions:Source (None, DeviceStatus, OrefWasm)
        var predictionSource = configuration.GetValue<PredictionSource>(
            "Predictions:Source",
            PredictionSource.None
        );
        switch (predictionSource)
        {
            case PredictionSource.DeviceStatus:
                services.AddScoped<IPredictionService, DeviceStatusPredictionService>();
                break;
            case PredictionSource.OrefWasm:
                services.AddScoped<IPredictionService, PredictionService>();
                services.AddOrefService(options =>
                {
                    options.WasmPath = "oref.wasm";
                    options.Enabled = true;
                });
                break;
            case PredictionSource.None:
            default:
                break;
        }

        services.AddScoped<ICobService, CobService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IAr2Service, Ar2Service>();
        services.AddScoped<IBolusWizardService, BolusWizardService>();

        services.AddScoped<IAuthorizationService, AuthorizationService>();
        services.AddScoped<IAlexaService, AlexaService>();

        services.AddScoped<IStatisticsService, StatisticsService>();

        // Analytics
        services.Configure<AnalyticsConfiguration>(
            configuration.GetSection(AnalyticsConfiguration.SectionName)
        );
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IConnectorHealthService, ConnectorHealthService>();

        return services;
    }

    /// <summary>
    /// Authentication, authorization, identity providers, multitenancy,
    /// and auth middleware handlers.
    /// </summary>
    public static IServiceCollection AddAuthenticationAndIdentity(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Options
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<OidcOptions>(configuration.GetSection(OidcOptions.SectionName));
        services.Configure<LocalIdentityOptions>(
            configuration.GetSection(LocalIdentityOptions.SectionName)
        );
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        // Auth services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<ISubjectService, SubjectService>();
        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IOidcProviderService, OidcProviderService>();
        services.AddScoped<IOidcAuthService, OidcAuthService>();

        // Local identity provider
        services.AddScoped<ILocalIdentityService, LocalIdentityService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddHostedService<UserSeedService>();
        services.AddHostedService<AuthorizationSeedService>();

        // Multitenancy
        services.Configure<MultitenancyConfiguration>(
            configuration.GetSection(MultitenancyConfiguration.SectionName)
        );
        services.AddScoped<ITenantAccessor, HttpContextTenantAccessor>();
        services.AddScoped<ITenantMemberService, TenantMemberService>();
        services.AddScoped<ITenantService, TenantService>();

        // Auth handlers (executed in priority order, lowest first)
        services.AddSingleton<IAuthHandler, SessionCookieHandler>(); // Priority 50
        services.AddSingleton<IAuthHandler, OidcTokenHandler>(); // Priority 100
        services.AddSingleton<IAuthHandler, LegacyJwtHandler>(); // Priority 200
        services.AddSingleton<IAuthHandler, AccessTokenHandler>(); // Priority 300
        services.AddSingleton<IAuthHandler, ApiSecretHandler>(); // Priority 400

        // OIDC provider discovery HTTP client
        services.AddHttpClient(
            "OidcProvider",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }
        );

        return services;
    }

    /// <summary>
    /// Domain CRUD services for entries, treatments, device status, profiles,
    /// food, activities, trackers, and all other data-owning services.
    /// </summary>
    public static IServiceCollection AddDomainServices(this IServiceCollection services)
    {
        // Demo mode
        services.AddSingleton<IDemoModeService, DemoModeService>();

        // V4 projection (must be registered before EntryService/TreatmentService)
        services.AddScoped<IV4ToLegacyProjectionService, V4ToLegacyProjectionService>();

        // Core domain services
        services.AddScoped<ITreatmentService, TreatmentService>();
        services.AddScoped<IEntryService, EntryService>();
        services.AddScoped<IStateSpanService, StateSpanService>();
        services.AddScoped<IDeviceStatusService, DeviceStatusService>();
        services.AddScoped<IBatteryService, BatteryService>();
        services.AddScoped<IProfileDataService, ProfileDataService>();

        // Food services
        services.AddScoped<IFoodService, FoodService>();
        services.AddScoped<IConnectorFoodEntryService, ConnectorFoodEntryService>();
        services.AddScoped<ITreatmentFoodService, TreatmentFoodService>();
        services.AddScoped<IUserFoodFavoriteService, UserFoodFavoriteService>();
        services.AddScoped<IConnectorFoodEntryRepository, ConnectorFoodEntryRepository>();
        services.AddScoped<IMealMatchingService, MealMatchingService>();

        // Activity and health metric services
        services.AddScoped<IActivityService, ActivityService>();
        services.AddScoped<IHeartRateService, HeartRateService>();
        services.AddScoped<IStepCountService, StepCountService>();

        // Tracker services
        services.AddScoped<ITrackerTriggerService, TrackerTriggerService>();
        services.AddScoped<ITrackerAlertService, TrackerAlertService>();
        services.AddScoped<ITrackerSuggestionService, TrackerSuggestionService>();
        services.AddScoped<IDeviceAgeService, DeviceAgeService>();

        // Device resolution
        services.AddScoped<IDeviceService, DeviceService>();

        // UI and display
        services.AddScoped<IUISettingsService, UISettingsService>();
        services.AddScoped<
            IMyFitnessPalMatchingSettingsService,
            MyFitnessPalMatchingSettingsService
        >();
        services.AddScoped<IClockFaceService, ClockFaceService>();
        services.AddScoped<IChartDataService, ChartDataService>();
        services.AddScoped<IDataOverviewService, DataOverviewService>();

        return services;
    }

    /// <summary>
    /// V4 repositories, snapshot repositories, profile repositories,
    /// patient record repositories, AID detection, and decomposition pipeline.
    /// </summary>
    public static IServiceCollection AddV4Infrastructure(this IServiceCollection services)
    {
        // V4 Repositories
        services.AddScoped<ISensorGlucoseRepository, SensorGlucoseRepository>();
        services.AddScoped<IMeterGlucoseRepository, MeterGlucoseRepository>();
        services.AddScoped<ICalibrationRepository, CalibrationRepository>();
        services.AddScoped<IBolusRepository, BolusRepository>();
        services.AddScoped<ITempBasalRepository, TempBasalRepository>();
        services.AddScoped<ICarbIntakeRepository, CarbIntakeRepository>();
        services.AddScoped<IBGCheckRepository, BGCheckRepository>();
        services.AddScoped<INoteRepository, NoteRepository>();
        services.AddScoped<IDeviceEventRepository, DeviceEventRepository>();
        services.AddScoped<IBolusCalculationRepository, BolusCalculationRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();

        // V4 Snapshot Repositories
        services.AddScoped<IApsSnapshotRepository, ApsSnapshotRepository>();
        services.AddScoped<IPumpSnapshotRepository, PumpSnapshotRepository>();
        services.AddScoped<IUploaderSnapshotRepository, UploaderSnapshotRepository>();

        // V4 Profile Repositories
        services.AddScoped<ITherapySettingsRepository, TherapySettingsRepository>();
        services.AddScoped<IBasalScheduleRepository, BasalScheduleRepository>();
        services.AddScoped<ICarbRatioScheduleRepository, CarbRatioScheduleRepository>();
        services.AddScoped<ISensitivityScheduleRepository, SensitivityScheduleRepository>();
        services.AddScoped<ITargetRangeScheduleRepository, TargetRangeScheduleRepository>();

        // V4 Patient Record Repositories
        services.AddScoped<IPatientRecordRepository, PatientRecordRepository>();
        services.AddScoped<IPatientDeviceRepository, PatientDeviceRepository>();
        services.AddScoped<IPatientInsulinRepository, PatientInsulinRepository>();

        // AID Detection Strategies and Metrics Service
        services.AddSingleton<IAidDetectionStrategy, ApsSnapshotStrategy>();
        services.AddSingleton<IAidDetectionStrategy, TbrBasedStrategy>();
        services.AddSingleton<IAidDetectionStrategy, NoAidStrategy>();
        services.AddScoped<IAidMetricsService, AidMetricsService>();

        // V4 Decomposers
        services.AddScoped<IEntryDecomposer, EntryDecomposer>();
        services.AddScoped<ITreatmentDecomposer, TreatmentDecomposer>();
        services.AddScoped<IDeviceStatusDecomposer, DeviceStatusDecomposer>();
        services.AddScoped<IActivityDecomposer, ActivityDecomposer>();
        services.AddScoped<IProfileDecomposer, ProfileDecomposer>();

        // Unified generic decomposer registrations
        services.AddScoped<IDecomposer<Entry>>(sp =>
            (IDecomposer<Entry>)sp.GetRequiredService<IEntryDecomposer>()
        );
        services.AddScoped<IDecomposer<Treatment>>(sp =>
            (IDecomposer<Treatment>)sp.GetRequiredService<ITreatmentDecomposer>()
        );
        services.AddScoped<IDecomposer<DeviceStatus>>(sp =>
            (IDecomposer<DeviceStatus>)sp.GetRequiredService<IDeviceStatusDecomposer>()
        );
        services.AddScoped<IDecomposer<Activity>>(sp =>
            (IDecomposer<Activity>)sp.GetRequiredService<IActivityDecomposer>()
        );
        services.AddScoped<IDecomposer<Profile>>(sp =>
            (IDecomposer<Profile>)sp.GetRequiredService<IProfileDecomposer>()
        );
        services.AddScoped<IDecompositionPipeline, DecompositionPipeline>();

        services.AddScoped<V4BackfillService>();

        return services;
    }

    /// <summary>
    /// Real-time communication (SignalR), notifications (in-app, push, Loop/OpenAPS),
    /// and the notification resolution background service.
    /// </summary>
    public static IServiceCollection AddRealTimeAndNotifications(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // SignalR
        services.AddSignalR();
        services.AddSingleton<
            Microsoft.AspNetCore.SignalR.IHubFilter,
            Nocturne.API.Hubs.TenantHubFilter
        >();
        services.AddScoped<ISignalRBroadcastService, SignalRBroadcastService>();

        // Push notifications
        services.AddScoped<INotificationV2Service, NotificationV2Service>();
        services.AddScoped<INotificationV1Service, NotificationV1Service>();
        services.AddScoped<IApnsClientFactory, ApnsClientFactory>();
        services.AddHttpClient(
            "dotAPNS",
            client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
            }
        );

        // Loop/OpenAPS integration
        services.Configure<LoopConfiguration>(options =>
        {
            options.ApnsKey = Environment.GetEnvironmentVariable("LOOP_APNS_KEY");
            options.ApnsKeyId = Environment.GetEnvironmentVariable("LOOP_APNS_KEY_ID");
            options.DeveloperTeamId = Environment.GetEnvironmentVariable(
                "LOOP_DEVELOPER_TEAM_ID"
            );
            options.PushServerEnvironment =
                Environment.GetEnvironmentVariable("LOOP_PUSH_SERVER_ENVIRONMENT") ?? "development";
        });
        services.AddScoped<ILoopService, LoopService>();
        services.AddScoped<IOpenApsService, OpenApsService>();
        services.AddScoped<IPumpAlertService, PumpAlertService>();

        // In-app notifications
        services.AddScoped<InAppNotificationRepository>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddHostedService<NotificationResolutionService>();

        return services;
    }

    /// <summary>
    /// Alert engines, device health monitoring, compression low detection,
    /// and all notifier implementations (SignalR, webhook, Pushover).
    /// </summary>
    public static IServiceCollection AddAlertingAndMonitoring(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // Compression low detection
        services.AddScoped<ICompressionLowRepository, CompressionLowRepository>();
        services.AddScoped<ICompressionLowService, CompressionLowService>();
        services.AddSingleton<CompressionLowDetectionService>();
        services.AddSingleton<ICompressionLowDetectionService>(sp =>
            sp.GetRequiredService<CompressionLowDetectionService>()
        );
        services.AddHostedService(sp => sp.GetRequiredService<CompressionLowDetectionService>());

        // Alert monitoring
        services.Configure<AlertMonitoringOptions>(
            configuration.GetSection(AlertMonitoringOptions.SectionName)
        );
        services.AddScoped<WebhookRequestSender>();
        services.AddScoped<IAlertRulesEngine, AlertRulesEngine>();
        services.AddScoped<IAlertProcessingService, AlertProcessingService>();
        services.AddScoped<IAlertOrchestrator, AlertOrchestrator>();
        // Notifier dispatch
        services.AddScoped<INotifierDispatcher, NotifierDispatcher>();
        services.AddScoped<INotifier, SignalRNotifier>();
        services.AddScoped<INotifier, WebhookNotifier>();

        // Pushover (conditional)
        var pushoverApiToken =
            configuration[ServiceNames.ConfigKeys.PushoverApiToken]
            ?? configuration[ServiceNames.ConfigKeys.PushoverApiTokenEnv];
        var pushoverUserKey =
            configuration[ServiceNames.ConfigKeys.PushoverUserKey]
            ?? configuration[ServiceNames.ConfigKeys.PushoverUserKeyEnv];

        if (
            !string.IsNullOrWhiteSpace(pushoverApiToken)
            && !string.IsNullOrWhiteSpace(pushoverUserKey)
        )
        {
            services.AddHttpClient<IPushoverService, PushoverService>();
            services.AddScoped<INotifier, PushoverNotifier>();
        }

        return services;
    }

    /// <summary>
    /// Data source connectors, deduplication, secret encryption,
    /// connector sync, and demo service health monitoring.
    /// </summary>
    public static IServiceCollection AddConnectorInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<IDataSourceService, DataSourceService>();
        services.AddScoped<IDeduplicationService, DeduplicationService>();
        services.AddSingleton<ISecretEncryptionService, SecretEncryptionService>();
        services.AddScoped<IConnectorConfigurationService, ConnectorConfigurationService>();
        services.AddScoped<IConnectorSyncService, ConnectorSyncService>();

        // Connector runtime
        services.AddBaseConnectorServices();
        services.AddScoped<IConnectorPublisher, InProcessConnectorPublisher>();
        services.AddConnectors(
            configuration,
            backgroundServiceAssembly: typeof(Program).Assembly
        );

        // Demo service health monitor
        services.AddHttpClient("DemoServiceHealth");
        services.AddHostedService<DemoServiceHealthMonitor>();

        return services;
    }

    /// <summary>
    /// Migration job service and startup migration check.
    /// </summary>
    public static IServiceCollection AddMigrationServices(this IServiceCollection services)
    {
        services.AddSingleton<
            Nocturne.API.Services.Migration.IMigrationJobService,
            Nocturne.API.Services.Migration.MigrationJobService
        >();
        services.AddHostedService<Nocturne.API.Services.Migration.MigrationStartupService>();

        return services;
    }
}

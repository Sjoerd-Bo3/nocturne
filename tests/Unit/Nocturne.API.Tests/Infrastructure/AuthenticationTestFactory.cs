using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Core.Contracts;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Data;
using Nocturne.Core.Contracts.Repositories;

namespace Nocturne.API.Tests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for authentication tests that mocks external dependencies
/// </summary>
public class AuthenticationTestFactory : WebApplicationFactory<Nocturne.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(
            (context, config) =>
            {
                // Clear existing configuration and add test-specific settings
                config.Sources.Clear();

                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        // Test environment
                        ["Environment"] = "Testing",

                        // Disable features that require external dependencies
                        ["Features:EnableExternalConnectors"] = "false",
                        ["Features:EnableRealTimeNotifications"] = "false",

                        // Use in-memory database for testing
                        ["ConnectionStrings:DefaultConnection"] = "Data Source=:memory:",

                        // Minimal logging for tests
                        ["Logging:LogLevel:Default"] = "Error",
                        ["Logging:LogLevel:Microsoft"] = "Error",
                        ["Logging:LogLevel:System"] = "Error",

                        // Don't set a default API_SECRET here - let tests override it
                        // ["API_SECRET"] = "",
                        ["JWT_SECRET"] = "test-jwt-secret-for-authentication-tests",
                    }
                );
            }
        );

        builder.ConfigureServices(services =>
        {
            // Remove database-related services that cause issues in tests
            RemoveService<ICacheService>(services);
            RemoveService<IEntryRepository>(services);
            RemoveService<ITreatmentRepository>(services);
            RemoveService<IProfileRepository>(services);
            RemoveService<IDeviceStatusRepository>(services);
            RemoveService<IFoodRepository>(services);
            RemoveService<IActivityRepository>(services);
            RemoveService<ISettingsRepository>(services);

            // Remove Entity Framework DbContext and related services to prevent migrations
            var dbContextServices = services
                .Where(s =>
                    s.ServiceType.Name.Contains("DbContext")
                    || s.ServiceType.Name.Contains("Migration")
                    || s.ServiceType.Name.Contains("Database")
                )
                .ToList();
            foreach (var service in dbContextServices)
            {
                services.Remove(service);
            }

            var sqliteConnection = new SqliteConnection("DataSource=:memory:");
            sqliteConnection.Open();

            services.AddDbContext<NocturneDbContext>(options =>
                options.UseSqlite(sqliteConnection)
                    .ConfigureWarnings(w =>
                        w.Ignore(RelationalEventId.PendingModelChangesWarning))
            );

            // Build a temporary service provider to create the schema from the model
            // (bypasses migrations which contain PostgreSQL-specific SQL)
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NocturneDbContext>();
            db.Database.EnsureCreated();

            // Seed a default tenant so tenant resolution middleware doesn't return 404
            if (!db.Tenants.Any())
            {
                db.Tenants.Add(new Nocturne.Infrastructure.Data.Entities.TenantEntity
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Slug = "default",
                    DisplayName = "Default",
                    IsActive = true,
                    IsDefault = true,
                });
                db.SaveChanges();
            }

            // Register IDbContextFactory that returns SQLite-backed contexts
            var mockDbContextFactory = new Mock<IDbContextFactory<NocturneDbContext>>();
            mockDbContextFactory
                .Setup(f => f.CreateDbContext())
                .Returns(() =>
                {
                    var opts = new DbContextOptionsBuilder<NocturneDbContext>()
                        .UseSqlite(sqliteConnection)
                        .ConfigureWarnings(w =>
                            w.Ignore(RelationalEventId.PendingModelChangesWarning))
                        .Options;
                    return new NocturneDbContext(opts);
                });
            mockDbContextFactory
                .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => mockDbContextFactory.Object.CreateDbContext());
            services.AddSingleton<IDbContextFactory<NocturneDbContext>>(mockDbContextFactory.Object);

            // Add mock cache service
            var mockCacheService = new Mock<ICacheService>();
            mockCacheService
                .Setup(x => x.GetAsync<object>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((object?)null);
            mockCacheService
                .Setup(x =>
                    x.SetAsync(
                        It.IsAny<string>(),
                        It.IsAny<object>(),
                        It.IsAny<TimeSpan?>(),
                        It.IsAny<CancellationToken>()
                    )
                )
                .Returns(Task.CompletedTask);

            services.AddSingleton(mockCacheService.Object);

            // Mock repository port interfaces
            var mockEntryRepository = new Mock<IEntryRepository>();
            mockEntryRepository
                .Setup(x => x.GetCurrentEntryAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync((Core.Models.Entry?)null);
            services.AddSingleton(mockEntryRepository.Object);

            services.AddSingleton(new Mock<ITreatmentRepository>().Object);
            services.AddSingleton(new Mock<IProfileRepository>().Object);
            services.AddSingleton(new Mock<IDeviceStatusRepository>().Object);
            services.AddSingleton(new Mock<IFoodRepository>().Object);
            services.AddSingleton(new Mock<IActivityRepository>().Object);
            services.AddSingleton(new Mock<ISettingsRepository>().Object);

            // Mock authorization service
            var mockAuthorizationService = new Mock<IAuthorizationService>();
            services.AddSingleton(mockAuthorizationService.Object);

            // Replace with in-memory cache
            services.AddMemoryCache();

            // Mock background services that might interfere with tests
            RemoveHostedServices(services);

            // Remove any database migration services
            // RemoveService<Microsoft.EntityFrameworkCore.Infrastructure.IDbContextFactory<object>>(services);
        });

        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            // Only add console logging in debug mode
#if DEBUG
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Warning);
#endif
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptors = services.Where(d => d.ServiceType == typeof(T)).ToList();
        foreach (var descriptor in descriptors)
        {
            services.Remove(descriptor);
        }
    }

    private static void RemoveHostedServices(IServiceCollection services)
    {
        // Remove background services that might cause issues in tests
        var hostedServices = services
            .Where(x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService))
            .ToList();
        foreach (var service in hostedServices)
        {
            services.Remove(service);
        }
    }
}

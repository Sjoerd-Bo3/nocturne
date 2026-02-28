using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.V4;
using Nocturne.Core.Contracts.V4.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.V4;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Data.Abstractions;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for TreatmentService domain service with WebSocket broadcasting
/// </summary>
[Parity("api.treatments.test.js")]
public class TreatmentServiceTests
{
    private readonly Mock<IPostgreSqlService> _mockPostgreSqlService;
    private readonly Mock<ISignalRBroadcastService> _mockBroadcastService;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IOptions<CacheConfiguration>> _mockCacheConfig;
    private readonly Mock<IDemoModeService> _mockDemoModeService;
    private readonly Mock<IStateSpanService> _mockStateSpanService;
    private readonly Mock<ITreatmentDecomposer> _mockTreatmentDecomposer;
    private readonly Mock<IV4ToLegacyProjectionService> _mockProjectionService;
    private readonly Mock<ITempBasalRepository> _mockTempBasalRepository;
    private readonly Mock<ILogger<TreatmentService>> _mockLogger;
    private readonly TreatmentService _treatmentService;

    public TreatmentServiceTests()
    {
        _mockPostgreSqlService = new Mock<IPostgreSqlService>();
        _mockBroadcastService = new Mock<ISignalRBroadcastService>();
        _mockCacheService = new Mock<ICacheService>();
        _mockCacheConfig = new Mock<IOptions<CacheConfiguration>>();
        _mockDemoModeService = new Mock<IDemoModeService>();
        _mockStateSpanService = new Mock<IStateSpanService>();
        _mockTreatmentDecomposer = new Mock<ITreatmentDecomposer>();
        _mockProjectionService = new Mock<IV4ToLegacyProjectionService>();
        _mockTempBasalRepository = new Mock<ITempBasalRepository>();
        _mockLogger = new Mock<ILogger<TreatmentService>>();

        _mockCacheConfig.Setup(x => x.Value).Returns(new CacheConfiguration());
        _mockDemoModeService.Setup(x => x.IsEnabled).Returns(false);
        _mockTempBasalRepository
            .Setup(x =>
                x.GetAsync(
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<TempBasal>());

        var mockTenantAccessor = new Mock<ITenantAccessor>();
        mockTenantAccessor.Setup(x => x.Context).Returns(new TenantContext(
            Guid.Parse("00000000-0000-0000-0000-000000000001"), "test-tenant", "Test Tenant", true));
        mockTenantAccessor.Setup(x => x.IsResolved).Returns(true);
        mockTenantAccessor.Setup(x => x.TenantId).Returns(Guid.Parse("00000000-0000-0000-0000-000000000001"));

        _treatmentService = new TreatmentService(
            _mockPostgreSqlService.Object,
            _mockBroadcastService.Object,
            _mockCacheService.Object,
            _mockCacheConfig.Object,
            _mockDemoModeService.Object,
            _mockStateSpanService.Object,
            _mockTreatmentDecomposer.Object,
            _mockProjectionService.Object,
            _mockTempBasalRepository.Object,
            mockTenantAccessor.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task GetTreatmentsAsync_ShouldCallMongoDbService()
    {
        // Arrange
        var expectedTreatments = new List<Treatment>
        {
            new Treatment
            {
                Id = "1",
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 50,
            },
            new Treatment
            {
                Id = "2",
                EventType = "Correction Bolus",
                Insulin = 2.0,
            },
        };

        // Production code now uses GetTreatmentsWithAdvancedFilterAsync with demo mode filter
        _mockPostgreSqlService
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(expectedTreatments);

        // Setup cache service to execute the factory function and return the result
        _mockCacheService
            .Setup(x =>
                x.GetOrSetAsync(
                    It.IsAny<string>(),
                    It.IsAny<Func<Task<List<Treatment>>>>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns<string, Func<Task<List<Treatment>>>, TimeSpan, CancellationToken>(
                async (key, factory, ttl, ct) => await factory()
            );

        // Act
        var result = await _treatmentService.GetTreatmentsAsync(
            count: 10,
            skip: 0,
            cancellationToken: CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);
        _mockPostgreSqlService.Verify(
            x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateTreatmentsAsync_ShouldBroadcastStorageCreateEvents()
    {
        // Arrange
        var treatmentsToCreate = new List<Treatment>
        {
            new Treatment
            {
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 50,
            },
            new Treatment { EventType = "Correction Bolus", Insulin = 2.0 },
        };
        var createdTreatments = new List<Treatment>
        {
            new Treatment
            {
                Id = "1",
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 50,
            },
            new Treatment
            {
                Id = "2",
                EventType = "Correction Bolus",
                Insulin = 2.0,
            },
        };

        _mockPostgreSqlService
            .Setup(x => x.CreateTreatmentsAsync(treatmentsToCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTreatments);

        // Act
        var result = await _treatmentService.CreateTreatmentsAsync(
            treatmentsToCreate,
            CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(2);

        // Verify MongoDB service was called
        _mockPostgreSqlService.Verify(
            x => x.CreateTreatmentsAsync(treatmentsToCreate, It.IsAny<CancellationToken>()),
            Times.Once
        ); // Verify WebSocket broadcasting was called for each treatment
        _mockBroadcastService.Verify(
            x =>
                x.BroadcastStorageCreateAsync(
                    "treatments",
                    It.Is<object>(o => o.ToString()!.Contains("treatments"))
                ),
            Times.Exactly(2)
        );
    }

    [Fact]
    public async Task UpdateTreatmentAsync_ShouldBroadcastStorageUpdateEvent()
    {
        // Arrange
        var treatmentId = "test-id";
        var treatmentToUpdate = new Treatment
        {
            EventType = "Meal Bolus",
            Insulin = 5.0,
            Carbs = 50,
        };
        var updatedTreatment = new Treatment
        {
            Id = treatmentId,
            EventType = "Meal Bolus",
            Insulin = 5.0,
            Carbs = 50,
        };

        _mockPostgreSqlService
            .Setup(x =>
                x.UpdateTreatmentAsync(
                    treatmentId,
                    treatmentToUpdate,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(updatedTreatment);

        // Act
        var result = await _treatmentService.UpdateTreatmentAsync(
            treatmentId,
            treatmentToUpdate,
            CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(treatmentId);

        // Verify PostgreSql service was called
        _mockPostgreSqlService.Verify(
            x =>
                x.UpdateTreatmentAsync(
                    treatmentId,
                    treatmentToUpdate,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Verify WebSocket broadcasting was called
        _mockBroadcastService.Verify(
            x =>
                x.BroadcastStorageUpdateAsync(
                    "treatments",
                    It.Is<object>(o => o.ToString()!.Contains("treatments"))
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task DeleteTreatmentAsync_ShouldBroadcastStorageDeleteEvent()
    {
        // Arrange
        var treatmentId = "test-id";
        var treatmentToDelete = new Treatment
        {
            Id = treatmentId,
            EventType = "Meal Bolus",
            Insulin = 5.0,
            Carbs = 50,
        };

        _mockPostgreSqlService
            .Setup(x => x.GetTreatmentByIdAsync(treatmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatmentToDelete);
        _mockPostgreSqlService
            .Setup(x => x.DeleteTreatmentAsync(treatmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _treatmentService.DeleteTreatmentAsync(
            treatmentId,
            CancellationToken.None
        );

        // Assert
        result.Should().BeTrue();

        // Verify PostgreSql service was called
        _mockPostgreSqlService.Verify(
            x => x.GetTreatmentByIdAsync(treatmentId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockPostgreSqlService.Verify(
            x => x.DeleteTreatmentAsync(treatmentId, It.IsAny<CancellationToken>()),
            Times.Once
        ); // Verify WebSocket broadcasting was called
        _mockBroadcastService.Verify(
            x =>
                x.BroadcastStorageDeleteAsync(
                    "treatments",
                    It.Is<object>(o => o.ToString()!.Contains("treatments"))
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task CreateTreatmentsAsync_ShouldHandleBroadcastException()
    {
        // Arrange
        var treatmentsToCreate = new List<Treatment>
        {
            new Treatment
            {
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 50,
            },
        };
        var createdTreatments = new List<Treatment>
        {
            new Treatment
            {
                Id = "1",
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 50,
            },
        };

        _mockPostgreSqlService
            .Setup(x => x.CreateTreatmentsAsync(treatmentsToCreate, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdTreatments);

        // Setup broadcast service to throw exception
        _mockBroadcastService
            .Setup(x => x.BroadcastStorageCreateAsync(It.IsAny<string>(), It.IsAny<object>()))
            .ThrowsAsync(new Exception("Broadcast failed"));

        // Act
        var result = await _treatmentService.CreateTreatmentsAsync(
            treatmentsToCreate,
            CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainSingle();

        // Verify the treatment was still created despite broadcast failure
        _mockPostgreSqlService.Verify(
            x => x.CreateTreatmentsAsync(treatmentsToCreate, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockBroadcastService.Verify(
            x => x.BroadcastStorageCreateAsync("treatments", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateTreatmentAsync_WhenTreatmentNotFound_ShouldNotBroadcast()
    {
        // Arrange
        var treatmentId = "non-existent-id";
        var treatmentToUpdate = new Treatment
        {
            EventType = "Meal Bolus",
            Insulin = 5.0,
            Carbs = 50,
        };

        _mockPostgreSqlService
            .Setup(x =>
                x.UpdateTreatmentAsync(
                    treatmentId,
                    treatmentToUpdate,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((Treatment?)null);

        // Act
        var result = await _treatmentService.UpdateTreatmentAsync(
            treatmentId,
            treatmentToUpdate,
            CancellationToken.None
        );

        // Assert
        result.Should().BeNull();

        // Verify PostgreSql service was called
        _mockPostgreSqlService.Verify(
            x =>
                x.UpdateTreatmentAsync(
                    treatmentId,
                    treatmentToUpdate,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Verify WebSocket broadcasting was NOT called
        _mockBroadcastService.Verify(
            x => x.BroadcastStorageUpdateAsync(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never
        );
    }

    [Fact]
    public async Task DeleteTreatmentAsync_WhenTreatmentNotFound_ShouldNotBroadcast()
    {
        // Arrange
        var treatmentId = "non-existent-id";

        _mockPostgreSqlService
            .Setup(x => x.GetTreatmentByIdAsync(treatmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);
        _mockPostgreSqlService
            .Setup(x => x.DeleteTreatmentAsync(treatmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _treatmentService.DeleteTreatmentAsync(
            treatmentId,
            CancellationToken.None
        );

        // Assert
        result.Should().BeFalse();

        // Verify WebSocket broadcasting was NOT called
        _mockBroadcastService.Verify(
            x => x.BroadcastStorageDeleteAsync(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never
        );
    }

    #region V4 TempBasal Tests

    [Fact]
    public async Task CreateTreatmentsAsync_TempBasal_ShouldDecomposeToV4()
    {
        // Arrange
        var tempBasal = new Treatment
        {
            EventType = "Temp Basal",
            Mills = 1700000000000,
            Duration = 30,
            Rate = 1.5,
        };

        var createdTempBasal = new TempBasal
        {
            Id = Guid.NewGuid(),
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000 + (30 * 60 * 1000)).UtcDateTime,
            Rate = 1.5,
            DataSource = "nightscout",
        };

        var decompositionResult = new DecompositionResult();
        decompositionResult.CreatedRecords.Add(createdTempBasal);

        _mockTreatmentDecomposer
            .Setup(x =>
                x.DecomposeAsync(
                    It.Is<Treatment>(t => t.EventType == "Temp Basal"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(decompositionResult);

        // Act
        var result = await _treatmentService.CreateTreatmentsAsync(
            new List<Treatment> { tempBasal },
            CancellationToken.None
        );

        // Assert
        result.Should().ContainSingle();
        result.First().EventType.Should().Be("Temp Basal");

        // Verify decomposer was called for the temp basal
        _mockTreatmentDecomposer.Verify(
            x =>
                x.DecomposeAsync(
                    It.IsAny<Treatment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Verify PostgreSqlService.CreateTreatmentsAsync was NOT called (no regular treatments)
        _mockPostgreSqlService.Verify(
            x =>
                x.CreateTreatmentsAsync(
                    It.IsAny<IEnumerable<Treatment>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task CreateTreatmentsAsync_MixedBatch_ShouldSplitCorrectly()
    {
        // Arrange
        var tempBasal = new Treatment
        {
            EventType = "Temp Basal",
            Mills = 1700000000000,
            Duration = 30,
            Rate = 1.5,
        };
        var bolus = new Treatment { EventType = "Correction Bolus", Insulin = 2.0 };

        var createdTempBasal = new TempBasal
        {
            Id = Guid.NewGuid(),
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000 + (30 * 60 * 1000)).UtcDateTime,
            Rate = 1.5,
            DataSource = "nightscout",
        };

        var decompositionResult = new DecompositionResult();
        decompositionResult.CreatedRecords.Add(createdTempBasal);

        _mockTreatmentDecomposer
            .Setup(x =>
                x.DecomposeAsync(
                    It.Is<Treatment>(t => t.EventType == "Temp Basal"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(decompositionResult);

        // Return empty decomposition result for regular treatments (bolus)
        _mockTreatmentDecomposer
            .Setup(x =>
                x.DecomposeAsync(
                    It.Is<Treatment>(t => t.EventType != "Temp Basal"),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new DecompositionResult());

        var createdBolus = new Treatment
        {
            Id = "bolus-1",
            EventType = "Correction Bolus",
            Insulin = 2.0,
        };
        _mockPostgreSqlService
            .Setup(x =>
                x.CreateTreatmentsAsync(
                    It.Is<IEnumerable<Treatment>>(t =>
                        t.All(tr => tr.EventType == "Correction Bolus")
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(new List<Treatment> { createdBolus });

        // Act
        var result = (
            await _treatmentService.CreateTreatmentsAsync(
                new List<Treatment> { tempBasal, bolus },
                CancellationToken.None
            )
        ).ToList();

        // Assert
        result.Should().HaveCount(2);

        // Temp basal went through the decomposer
        _mockTreatmentDecomposer.Verify(
            x =>
                x.DecomposeAsync(
                    It.Is<Treatment>(t => t.EventType == "Temp Basal"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Bolus went through PostgreSqlService and then the decomposer
        _mockPostgreSqlService.Verify(
            x =>
                x.CreateTreatmentsAsync(
                    It.IsAny<IEnumerable<Treatment>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        // Decomposer was also called for the regular bolus treatment after legacy write
        _mockTreatmentDecomposer.Verify(
            x =>
                x.DecomposeAsync(
                    It.Is<Treatment>(t => t.EventType == "Correction Bolus"),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetTreatmentByIdAsync_WhenInTreatmentsTable_ShouldReturnDirectly()
    {
        // Arrange
        var treatmentId = "treatment-1";
        var expected = new Treatment
        {
            Id = treatmentId,
            EventType = "Meal Bolus",
            Insulin = 5.0,
        };

        _mockPostgreSqlService
            .Setup(x => x.GetTreatmentByIdAsync(treatmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _treatmentService.GetTreatmentByIdAsync(
            treatmentId,
            CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(treatmentId);
        result.EventType.Should().Be("Meal Bolus");

        // Should NOT check TempBasal since treatment was found in DB
        _mockTempBasalRepository.Verify(
            x => x.GetByLegacyIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    [Fact]
    public async Task GetTreatmentByIdAsync_WhenNotInTreatments_ShouldFallBackToTempBasal()
    {
        // Arrange
        var legacyId = "legacy-temp-basal-1";

        _mockPostgreSqlService
            .Setup(x => x.GetTreatmentByIdAsync(legacyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var tempBasal = new TempBasal
        {
            Id = Guid.NewGuid(),
            LegacyId = legacyId,
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700001800000).UtcDateTime, // +30 min
            Rate = 1.5,
            DataSource = "AAPS",
        };

        _mockTempBasalRepository
            .Setup(x => x.GetByLegacyIdAsync(legacyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempBasal);

        // Act
        var result = await _treatmentService.GetTreatmentByIdAsync(legacyId, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be("Temp Basal");
        result.Rate.Should().Be(1.5);
        result.Mills.Should().Be(1700000000000);
    }

    [Fact]
    public async Task GetTreatmentByIdAsync_WhenNotFoundAnywhere_ShouldReturnNull()
    {
        // Arrange
        var id = "non-existent";

        _mockPostgreSqlService
            .Setup(x => x.GetTreatmentByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        _mockTempBasalRepository
            .Setup(x => x.GetByLegacyIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TempBasal?)null);

        // Act
        var result = await _treatmentService.GetTreatmentByIdAsync(id, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task PatchTreatmentAsync_WhenNotFoundAnywhere_ShouldReturnNull()
    {
        // Arrange
        var id = "non-existent";
        var patchJson = JsonSerializer.Deserialize<JsonElement>("""{"duration": 45}""");

        _mockPostgreSqlService
            .Setup(x =>
                x.PatchTreatmentAsync(id, It.IsAny<JsonElement>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync((Treatment?)null);

        // Act
        var result = await _treatmentService.PatchTreatmentAsync(
            id,
            patchJson,
            CancellationToken.None
        );

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTreatmentsWithAdvancedFilterAsync_ShouldMergeTempBasals()
    {
        // Arrange
        var dbTreatments = new List<Treatment>
        {
            new Treatment
            {
                Id = "t1",
                EventType = "Meal Bolus",
                Mills = 1700000200000,
            },
            new Treatment
            {
                Id = "t2",
                EventType = "Correction Bolus",
                Mills = 1700000100000,
            },
        };

        _mockPostgreSqlService
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string?>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(dbTreatments);

        var tempBasals = new List<TempBasal>
        {
            new TempBasal
            {
                Id = Guid.NewGuid(),
                LegacyId = "s1",
                StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000150000).UtcDateTime,
                EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000150000 + (30 * 60 * 1000)).UtcDateTime,
                Rate = 1.5,
                DataSource = "AAPS",
            },
        };

        _mockTempBasalRepository
            .Setup(x =>
                x.GetAsync(
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(tempBasals);

        // Act
        var result = (
            await _treatmentService.GetTreatmentsWithAdvancedFilterAsync(
                count: 10,
                skip: 0,
                findQuery: null,
                reverseResults: false,
                cancellationToken: CancellationToken.None
            )
        ).ToList();

        // Assert - should have all 3, merged and sorted descending by Mills
        result.Should().HaveCount(3);
        result[0].Id.Should().Be("t1"); // Mills 200000 (highest)
        result[1].EventType.Should().Be("Temp Basal"); // Mills 150000 (temp basal from V4)
        result[2].Id.Should().Be("t2"); // Mills 100000 (lowest)
    }

    [Fact]
    public async Task GetTreatmentsModifiedSinceAsync_ShouldIncludeTempBasals()
    {
        // Arrange
        var dbTreatments = new List<Treatment>
        {
            new Treatment
            {
                Id = "t1",
                EventType = "Meal Bolus",
                Mills = 1700000100000,
            },
        };

        _mockPostgreSqlService
            .Setup(x =>
                x.GetTreatmentsModifiedSinceAsync(
                    It.IsAny<long>(),
                    It.IsAny<int>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(dbTreatments);

        var tempBasals = new List<TempBasal>
        {
            new TempBasal
            {
                Id = Guid.NewGuid(),
                LegacyId = "s1",
                StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000200000).UtcDateTime,
                EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000200000 + (30 * 60 * 1000)).UtcDateTime,
                Rate = 1.5,
                DataSource = "AAPS",
            },
        };

        _mockTempBasalRepository
            .Setup(x =>
                x.GetAsync(
                    It.IsAny<DateTime?>(),
                    It.IsAny<DateTime?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(tempBasals);

        // Act
        var result = (
            await _treatmentService.GetTreatmentsModifiedSinceAsync(
                lastModifiedMills: 1700000000000,
                limit: 500,
                cancellationToken: CancellationToken.None
            )
        ).ToList();

        // Assert - merged and sorted ascending by Mills
        result.Should().HaveCount(2);
        result[0].Id.Should().Be("t1"); // Mills 100000 (lowest first - ascending)
        result[1].EventType.Should().Be("Temp Basal"); // Mills 200000 (temp basal from V4)
    }

    [Fact]
    public async Task DeleteTreatmentAsync_TempBasal_ShouldDeleteFromV4()
    {
        // Arrange
        var legacyId = "legacy-temp-basal-1";
        var tempBasalId = Guid.NewGuid();

        var tempBasal = new TempBasal
        {
            Id = tempBasalId,
            LegacyId = legacyId,
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700001800000).UtcDateTime,
            Rate = 1.5,
            DataSource = "AAPS",
        };

        _mockTempBasalRepository
            .Setup(x => x.GetByLegacyIdAsync(legacyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tempBasal);

        // Act
        var result = await _treatmentService.DeleteTreatmentAsync(legacyId, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        // Verify TempBasal was deleted
        _mockTempBasalRepository.Verify(
            x => x.DeleteAsync(tempBasalId, It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Verify PostgreSqlService.DeleteTreatmentAsync was NOT called
        _mockPostgreSqlService.Verify(
            x => x.DeleteTreatmentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );

        // Verify broadcast was sent
        _mockBroadcastService.Verify(
            x => x.BroadcastStorageDeleteAsync("treatments", It.IsAny<object>()),
            Times.Once
        );
    }

    [Fact]
    public async Task UpdateTreatmentAsync_TempBasal_ShouldDecomposeAndUpdate()
    {
        // Arrange
        var legacyId = "legacy-temp-basal-1";
        var tempBasalId = Guid.NewGuid();
        var treatmentUpdate = new Treatment
        {
            EventType = "Temp Basal",
            Mills = 1700000000000,
            Duration = 45,
            Rate = 2.0,
        };

        var existingTempBasal = new TempBasal
        {
            Id = tempBasalId,
            LegacyId = legacyId,
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700001800000).UtcDateTime,
            Rate = 1.5,
            DataSource = "AAPS",
        };

        _mockTempBasalRepository
            .Setup(x => x.GetByLegacyIdAsync(legacyId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTempBasal);

        _mockTreatmentDecomposer
            .Setup(x => x.DecomposeAsync(It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DecompositionResult());

        var updatedTempBasal = new TempBasal
        {
            Id = tempBasalId,
            LegacyId = legacyId,
            StartTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000).UtcDateTime,
            EndTimestamp = DateTimeOffset.FromUnixTimeMilliseconds(1700000000000 + (45 * 60 * 1000)).UtcDateTime,
            Rate = 2.0,
            DataSource = "AAPS",
        };

        _mockTempBasalRepository
            .Setup(x => x.GetByIdAsync(tempBasalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedTempBasal);

        // Act
        var result = await _treatmentService.UpdateTreatmentAsync(
            legacyId,
            treatmentUpdate,
            CancellationToken.None
        );

        // Assert
        result.Should().NotBeNull();
        result!.EventType.Should().Be("Temp Basal");

        // Verify decomposer was called (not PostgreSql update)
        _mockTreatmentDecomposer.Verify(
            x =>
                x.DecomposeAsync(
                    It.IsAny<Treatment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _mockPostgreSqlService.Verify(
            x =>
                x.UpdateTreatmentAsync(
                    It.IsAny<string>(),
                    It.IsAny<Treatment>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    #endregion
}

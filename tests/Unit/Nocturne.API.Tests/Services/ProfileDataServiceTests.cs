using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Tests.Shared.Mocks;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for ProfileDataService domain service with WebSocket broadcasting
/// </summary>
public class ProfileDataServiceTests
{
    private readonly Mock<IProfileRepository> _mockProfileRepository;
    private readonly Mock<IWriteSideEffects> _mockSideEffects;
    private readonly Mock<ICacheService> _mockCacheService;
    private readonly Mock<IOptions<CacheConfiguration>> _mockCacheConfig;
    private readonly Mock<ILogger<ProfileDataService>> _mockLogger;
    private readonly ProfileDataService _profileDataService;

    public ProfileDataServiceTests()
    {
        _mockProfileRepository = new Mock<IProfileRepository>();
        _mockSideEffects = new Mock<IWriteSideEffects>();
        _mockCacheService = new Mock<ICacheService>();
        _mockCacheConfig = new Mock<IOptions<CacheConfiguration>>();
        _mockLogger = new Mock<ILogger<ProfileDataService>>();

        _mockCacheConfig.Setup(x => x.Value).Returns(new CacheConfiguration());

        _profileDataService = new ProfileDataService(
            _mockProfileRepository.Object,
            _mockSideEffects.Object,
            Mock.Of<IDataEventSink<Profile>>(),
            _mockCacheService.Object,
            _mockCacheConfig.Object,
            MockTenantAccessor.Create().Object,
            _mockLogger.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetProfilesAsync_WithoutParameters_ReturnsAllProfiles()
    {
        // Arrange
        var expectedProfiles = new List<Profile>
        {
            new Profile
            {
                Id = "1",
                DefaultProfile = "default",
                Mills = 1234567890,
            },
            new Profile
            {
                Id = "2",
                DefaultProfile = "work",
                Mills = 1234567880,
            },
        };
        _mockProfileRepository
            .Setup(x => x.GetProfilesAsync(10, 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProfiles);

        // Act
        var result = await _profileDataService.GetProfilesAsync(
            cancellationToken: CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        Assert.Equal(expectedProfiles, result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetProfilesAsync_WithParameters_ReturnsFilteredProfiles()
    {
        // Arrange
        var find = "{\"defaultProfile\":\"default\"}";
        var count = 10;
        var skip = 0;
        var expectedProfiles = new List<Profile>
        {
            new Profile
            {
                Id = "1",
                DefaultProfile = "default",
                Mills = 1234567890,
            },
        };
        _mockProfileRepository
            .Setup(x => x.GetProfilesAsync(count, skip, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProfiles);

        // Act
        var result = await _profileDataService.GetProfilesAsync(
            find,
            count,
            skip,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Single(result);
        Assert.Equal(expectedProfiles.First().Id, result.First().Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetProfileByIdAsync_WithValidId_ReturnsProfile()
    {
        // Arrange
        var profileId = "60a1b2c3d4e5f6789012345";
        var expectedProfile = new Profile
        {
            Id = profileId,
            DefaultProfile = "default",
            Mills = 1234567890,
        };

        _mockProfileRepository
            .Setup(x => x.GetProfileByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProfile);

        // Act
        var result = await _profileDataService.GetProfileByIdAsync(
            profileId,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(profileId, result.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetProfileByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var profileId = "invalidid";

        _mockProfileRepository
            .Setup(x => x.GetProfileByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        // Act
        var result = await _profileDataService.GetProfileByIdAsync(
            profileId,
            CancellationToken.None
        );

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateProfilesAsync_WithValidProfiles_ReturnsCreatedProfilesAndBroadcasts()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            new Profile { DefaultProfile = "default", Mills = 1234567890 },
            new Profile { DefaultProfile = "work", Mills = 1234567880 },
        };

        var createdProfiles = profiles
            .Select(p => new Profile
            {
                Id = Guid.NewGuid().ToString(),
                DefaultProfile = p.DefaultProfile,
                Mills = p.Mills,
            })
            .ToList();

        _mockProfileRepository
            .Setup(x => x.CreateProfilesAsync(profiles, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdProfiles);

        // Act
        var result = await _profileDataService.CreateProfilesAsync(
            profiles,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Count());
        _mockProfileRepository.Verify(
            x => x.CreateProfilesAsync(profiles, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnCreatedAsync(
                "profiles",
                It.IsAny<IReadOnlyList<Profile>>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateProfileAsync_WithValidProfile_ReturnsUpdatedProfileAndBroadcasts()
    {
        // Arrange
        var profileId = "60a1b2c3d4e5f6789012345";
        var profile = new Profile
        {
            Id = profileId,
            DefaultProfile = "default",
            Mills = 1234567890,
        };
        var updatedProfile = new Profile
        {
            Id = profileId,
            DefaultProfile = "updated",
            Mills = 1234567890,
        };

        _mockProfileRepository
            .Setup(x => x.UpdateProfileAsync(profileId, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedProfile);

        // Act
        var result = await _profileDataService.UpdateProfileAsync(
            profileId,
            profile,
            CancellationToken.None
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(profileId, result.Id);
        Assert.Equal("updated", result.DefaultProfile);
        _mockProfileRepository.Verify(
            x => x.UpdateProfileAsync(profileId, profile, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnUpdatedAsync(
                "profiles",
                It.IsAny<Profile>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task UpdateProfileAsync_WithInvalidId_ReturnsNullAndDoesNotBroadcast()
    {
        // Arrange
        var profileId = "invalidid";
        var profile = new Profile { DefaultProfile = "default", Mills = 1234567890 };

        _mockProfileRepository
            .Setup(x => x.UpdateProfileAsync(profileId, profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        // Act
        var result = await _profileDataService.UpdateProfileAsync(
            profileId,
            profile,
            CancellationToken.None
        );

        // Assert
        Assert.Null(result);
        _mockSideEffects.Verify(
            x => x.OnUpdatedAsync(
                It.IsAny<string>(),
                It.IsAny<Profile>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteProfileAsync_WithValidId_ReturnsTrueAndBroadcasts()
    {
        // Arrange
        var profileId = "60a1b2c3d4e5f6789012345";
        var profileToDelete = new Profile
        {
            Id = profileId,
            DefaultProfile = "default",
            Mills = 1234567890,
        };

        _mockProfileRepository
            .Setup(x => x.GetProfileByIdAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profileToDelete);

        _mockProfileRepository
            .Setup(x => x.DeleteProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _profileDataService.DeleteProfileAsync(
            profileId,
            CancellationToken.None
        );

        // Assert
        Assert.True(result);
        _mockProfileRepository.Verify(
            x => x.DeleteProfileAsync(profileId, It.IsAny<CancellationToken>()),
            Times.Once
        );
        _mockSideEffects.Verify(
            x => x.OnDeletedAsync(
                "profiles",
                It.IsAny<Profile>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteProfileAsync_WithInvalidId_ReturnsFalseAndDoesNotBroadcast()
    {
        // Arrange
        var profileId = "invalidid";

        _mockProfileRepository
            .Setup(x => x.DeleteProfileAsync(profileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _profileDataService.DeleteProfileAsync(
            profileId,
            CancellationToken.None
        );

        // Assert
        Assert.False(result);
        _mockSideEffects.Verify(
            x => x.OnDeletedAsync(
                It.IsAny<string>(),
                It.IsAny<Profile>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteProfilesAsync_WithValidFilter_ReturnsZero_NotYetImplemented()
    {
        // Arrange
        var find = "{\"defaultProfile\":\"default\"}";

        // Act
        var result = await _profileDataService.DeleteProfilesAsync(find, CancellationToken.None);

        // Assert
        Assert.Equal(0, result); // Current implementation returns 0 as bulk delete is not implemented
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task DeleteProfilesAsync_WithNoMatches_ReturnsZero_NotYetImplemented()
    {
        // Arrange
        var find = "{\"defaultProfile\":\"nonexistent\"}";

        // Act
        var result = await _profileDataService.DeleteProfilesAsync(find, CancellationToken.None);

        // Assert
        Assert.Equal(0, result); // Current implementation returns 0 as bulk delete is not implemented
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetCurrentProfileAsync_WithProfiles_ReturnsLatestProfile()
    {
        // Arrange
        var expectedProfile = new Profile
        {
            Id = "1",
            DefaultProfile = "default",
            Mills = 1234567890,
        };

        _mockCacheService
            .Setup(x => x.GetAsync<Profile>("profiles:current", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null); // Cache miss

        _mockProfileRepository
            .Setup(x => x.GetCurrentProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedProfile);

        // Act
        var result = await _profileDataService.GetCurrentProfileAsync(CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedProfile.Id, result.Id);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task GetCurrentProfileAsync_WithNoProfiles_ReturnsNull()
    {
        // Arrange
        _mockCacheService
            .Setup(x => x.GetAsync<Profile>("profiles:current", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null); // Cache miss

        _mockProfileRepository
            .Setup(x => x.GetCurrentProfileAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        // Act
        var result = await _profileDataService.GetCurrentProfileAsync(CancellationToken.None);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Category", "Parity")]
    public async Task CreateProfilesAsync_WithException_ThrowsException()
    {
        // Arrange
        var profiles = new List<Profile>
        {
            new Profile { DefaultProfile = "default", Mills = 1234567890 },
        };

        _mockProfileRepository
            .Setup(x => x.CreateProfilesAsync(profiles, It.IsAny<CancellationToken>()))
            .Throws(new InvalidOperationException("Processing failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _profileDataService.CreateProfilesAsync(profiles, CancellationToken.None)
        );
        _mockSideEffects.Verify(
            x => x.OnCreatedAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Profile>>(),
                It.IsAny<WriteEffectOptions>(),
                It.IsAny<CancellationToken>()
            ),
            Times.Never
        );
    }
}

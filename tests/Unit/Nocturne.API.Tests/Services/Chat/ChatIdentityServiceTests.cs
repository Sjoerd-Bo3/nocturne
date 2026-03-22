using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services.Chat;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Services.Chat;

[Trait("Category", "Unit")]
public class ChatIdentityServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _options;
    private readonly Mock<IDbContextFactory<NocturneDbContext>> _contextFactoryMock;
    private readonly ChatIdentityService _sut;

    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public ChatIdentityServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        using var initDb = new NocturneDbContext(_options);
        initDb.Database.EnsureCreated();

        _contextFactoryMock = new Mock<IDbContextFactory<NocturneDbContext>>();
        _contextFactoryMock
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new NocturneDbContext(_options));

        var loggerMock = new Mock<ILogger<ChatIdentityService>>();
        _sut = new ChatIdentityService(_contextFactoryMock.Object, loggerMock.Object);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }

    private async Task SeedLinkAsync(
        Guid? id = null,
        string platform = "discord",
        string platformUserId = "user-123",
        bool isActive = true,
        DateTime? revokedAt = null,
        Guid? tenantId = null,
        Guid? userId = null)
    {
        await using var db = new NocturneDbContext(_options);
        db.TenantId = tenantId ?? TenantId;
        db.ChatIdentityLinks.Add(new ChatIdentityLinkEntity
        {
            Id = id ?? Guid.CreateVersion7(),
            TenantId = tenantId ?? TenantId,
            NocturneUserId = userId ?? UserId,
            Platform = platform,
            PlatformUserId = platformUserId,
            IsActive = isActive,
            RevokedAt = revokedAt,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task FindByPlatformAsync_ReturnsActiveLink_WhenExists()
    {
        // Arrange
        await SeedLinkAsync(platform: "discord", platformUserId: "disc-42", isActive: true);

        // Act
        var result = await _sut.FindByPlatformAsync(TenantId, "discord", "disc-42", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.Platform.Should().Be("discord");
        result.PlatformUserId.Should().Be("disc-42");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task FindByPlatformAsync_ReturnsNull_WhenRevoked()
    {
        // Arrange
        await SeedLinkAsync(
            platform: "telegram",
            platformUserId: "tg-99",
            isActive: false,
            revokedAt: DateTime.UtcNow);

        // Act
        var result = await _sut.FindByPlatformAsync(TenantId, "telegram", "tg-99", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task FindByPlatformAsync_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _sut.FindByPlatformAsync(TenantId, "slack", "nonexistent", CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateLinkAsync_CreatesWithCorrectFields()
    {
        // Act
        var result = await _sut.CreateLinkAsync(
            TenantId, UserId, "discord", "disc-new", "channel-1", CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TenantId.Should().Be(TenantId);
        result.NocturneUserId.Should().Be(UserId);
        result.Platform.Should().Be("discord");
        result.PlatformUserId.Should().Be("disc-new");
        result.PlatformChannelId.Should().Be("channel-1");
        result.IsActive.Should().BeTrue();
        result.RevokedAt.Should().BeNull();

        // Verify persisted
        await using var db = new NocturneDbContext(_options);
        db.TenantId = TenantId;
        var persisted = await db.ChatIdentityLinks.FirstOrDefaultAsync(l => l.Id == result.Id);
        persisted.Should().NotBeNull();
    }

    [Fact]
    public async Task RevokeLinkAsync_SetsIsActiveFalseAndRevokedAt()
    {
        // Arrange
        var linkId = Guid.CreateVersion7();
        await SeedLinkAsync(id: linkId, isActive: true);

        // Act
        await _sut.RevokeLinkAsync(TenantId, linkId, CancellationToken.None);

        // Assert
        await using var db = new NocturneDbContext(_options);
        db.TenantId = TenantId;
        var link = await db.ChatIdentityLinks.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == linkId);
        link.Should().NotBeNull();
        link!.IsActive.Should().BeFalse();
        link.RevokedAt.Should().NotBeNull();
        link.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetByTenantAsync_ReturnsOnlyActiveLinks()
    {
        // Arrange
        await SeedLinkAsync(platform: "discord", platformUserId: "active-1", isActive: true);
        await SeedLinkAsync(platform: "telegram", platformUserId: "active-2", isActive: true);
        await SeedLinkAsync(platform: "slack", platformUserId: "revoked-1", isActive: false,
            revokedAt: DateTime.UtcNow);

        // Act
        var result = await _sut.GetByTenantAsync(TenantId, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(l => l.IsActive);
    }
}

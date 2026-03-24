using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class MemberInviteServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<IJwtService> _jwtService;
    private readonly MemberInviteService _service;

    private readonly Guid _tenantId = Guid.CreateVersion7();
    private readonly Guid _creatorSubjectId = Guid.CreateVersion7();
    private readonly Guid _acceptorSubjectId = Guid.CreateVersion7();

    private const string FakeToken = "fake-random-token-abc123";
    private const string FakeTokenHash = "hashed-fake-token";
    private const string BaseUrl = "https://app.nocturnecgm.com";

    public MemberInviteServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(_dbOptions);
        _dbContext.Database.EnsureCreated();

        _jwtService = new Mock<IJwtService>();
        _jwtService.Setup(j => j.GenerateRefreshToken()).Returns(FakeToken);
        _jwtService.Setup(j => j.HashRefreshToken(FakeToken)).Returns(FakeTokenHash);

        var oidcOptions = Options.Create(new OidcOptions
        {
            BaseUrl = BaseUrl,
        });

        var logger = new Mock<ILogger<MemberInviteService>>();

        _service = new MemberInviteService(
            _dbContext,
            _jwtService.Object,
            oidcOptions,
            logger.Object);

        // Seed tenant and subjects
        SeedData();
    }

    private void SeedData()
    {
        _dbContext.Tenants.Add(new TenantEntity
        {
            Id = _tenantId,
            Slug = "test",
            DisplayName = "Test Tenant",
        });

        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = _creatorSubjectId,
            Name = "Creator User",
        });

        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = _acceptorSubjectId,
            Name = "Acceptor User",
        });

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateInviteAsync_ReturnsTokenAndUrl()
    {
        var result = await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead, OAuthScopes.TreatmentsRead]);

        result.Token.Should().Be(FakeToken);
        result.InviteUrl.Should().Be($"{BaseUrl}/invite/{FakeToken}");
        result.Id.Should().NotBeEmpty();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        // Verify entity was persisted
        var entity = await _dbContext.MemberInvites.FirstOrDefaultAsync();
        entity.Should().NotBeNull();
        entity!.TokenHash.Should().Be(FakeTokenHash);
        entity.TenantId.Should().Be(_tenantId);
        entity.Role.Should().Be(TenantRole.Follower);
    }

    [Fact]
    public async Task CreateInviteAsync_FollowerRole_ValidatesReadOnlyScopes()
    {
        var act = () => _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead, OAuthScopes.EntriesReadWrite]);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Invalid scopes*read-only*");
    }

    [Fact]
    public async Task CreateInviteAsync_FollowerRole_RequiresAtLeastOneScope()
    {
        var act = () => _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: []);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*At least one scope*");
    }

    [Fact]
    public async Task CreateInviteAsync_NonFollowerRole_IgnoresScopes()
    {
        var result = await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Admin,
            scopes: [OAuthScopes.EntriesRead]);

        result.Token.Should().Be(FakeToken);

        var entity = await _dbContext.MemberInvites.FirstOrDefaultAsync();
        entity.Should().NotBeNull();
        entity!.Scopes.Should().BeNull();
        entity.Role.Should().Be(TenantRole.Admin);
    }

    [Fact]
    public async Task AcceptInviteAsync_ValidToken_CreatesMembership()
    {
        // Create invite
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead],
            label: "Test Invite",
            limitTo24Hours: true);

        // Accept invite
        var result = await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        result.Success.Should().BeTrue();
        result.MembershipId.Should().NotBeNull();
        result.ErrorCode.Should().BeNull();

        // Verify membership was created
        var member = await _dbContext.TenantMembers
            .FirstOrDefaultAsync(m => m.SubjectId == _acceptorSubjectId);
        member.Should().NotBeNull();
        member!.TenantId.Should().Be(_tenantId);
        member.Role.Should().Be(TenantRole.Follower);
        member.Scopes.Should().Contain(OAuthScopes.EntriesRead);
        member.Label.Should().Be("Test Invite");
        member.LimitTo24Hours.Should().BeTrue();
        member.CreatedFromInviteId.Should().NotBeNull();
    }

    [Fact]
    public async Task AcceptInviteAsync_ExpiredToken_ReturnsError()
    {
        // Create invite and manually expire it
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead]);

        var invite = await _dbContext.MemberInvites.FirstAsync();
        invite.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("expired");
    }

    [Fact]
    public async Task AcceptInviteAsync_RevokedToken_ReturnsError()
    {
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead]);

        var invite = await _dbContext.MemberInvites.FirstAsync();
        invite.RevokedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("revoked");
    }

    [Fact]
    public async Task AcceptInviteAsync_ExhaustedUses_ReturnsError()
    {
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead],
            maxUses: 1);

        var invite = await _dbContext.MemberInvites.FirstAsync();
        invite.UseCount = 1;
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("exhausted");
    }

    [Fact]
    public async Task AcceptInviteAsync_AlreadyMember_ReturnsError()
    {
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead]);

        // Add an existing active membership
        _dbContext.TenantMembers.Add(new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            SubjectId = _acceptorSubjectId,
            Role = TenantRole.Follower,
            RevokedAt = null,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("already_member");
    }

    [Fact]
    public async Task AcceptInviteAsync_IncrementsUseCount()
    {
        await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead]);

        await _service.AcceptInviteAsync(FakeToken, _acceptorSubjectId);

        var invite = await _dbContext.MemberInvites.FirstAsync();
        invite.UseCount.Should().Be(1);
    }

    [Fact]
    public async Task RevokeInviteAsync_SetsRevokedAt()
    {
        var createResult = await _service.CreateInviteAsync(
            _tenantId,
            _creatorSubjectId,
            TenantRole.Follower,
            scopes: [OAuthScopes.EntriesRead]);

        var result = await _service.RevokeInviteAsync(createResult.Id, _tenantId);

        result.Should().BeTrue();

        var invite = await _dbContext.MemberInvites.FirstAsync();
        invite.RevokedAt.Should().NotBeNull();
        invite.IsRevoked.Should().BeTrue();
    }
}

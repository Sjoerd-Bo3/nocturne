using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.API.Services.Auth;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

/// <summary>
/// Unit tests for RecoveryModeCheckService
/// </summary>
public class RecoveryModeCheckServiceTests : IDisposable
{
    private readonly NocturneDbContext _dbContext;
    private readonly RecoveryModeState _state;
    private readonly ServiceProvider _serviceProvider;

    public RecoveryModeCheckServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryContext();
        _state = new RecoveryModeState();

        var services = new ServiceCollection();
        services.AddSingleton(_dbContext);
        // Register NocturneDbContext so CreateScope can resolve it
        services.AddScoped<NocturneDbContext>(_ => _dbContext);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _dbContext.Dispose();
    }

    private RecoveryModeCheckService CreateService()
    {
        return new RecoveryModeCheckService(
            _serviceProvider,
            _state,
            NullLogger<RecoveryModeCheckService>.Instance
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_NoOrphanedSubjects_DoesNotEnableRecoveryMode()
    {
        // No subjects at all
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_SubjectWithPasskey_NotOrphaned()
    {
        var subjectId = Guid.CreateVersion7();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = "Test User",
            IsActive = true,
            IsSystemSubject = false,
        });
        _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = Guid.CreateVersion7(),
            SubjectId = subjectId,
            CredentialId = new byte[] { 1, 2, 3 },
            PublicKey = new byte[] { 4, 5, 6 },
            SignCount = 0,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_SubjectWithOidc_NotOrphaned()
    {
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "OIDC User",
            IsActive = true,
            IsSystemSubject = false,
            OidcSubjectId = "oidc-sub-123",
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_SubjectWithNeither_OrphanedEnablesRecoveryMode()
    {
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Orphaned User",
            IsActive = true,
            IsSystemSubject = false,
            OidcSubjectId = null,
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_SystemSubjectWithNeither_Ignored()
    {
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "System Subject",
            IsActive = true,
            IsSystemSubject = true,
            OidcSubjectId = null,
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_InactiveSubjectWithNeither_Ignored()
    {
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = Guid.CreateVersion7(),
            Name = "Inactive User",
            IsActive = false,
            IsSystemSubject = false,
            OidcSubjectId = null,
        });
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        await service.StartAsync(CancellationToken.None);

        _state.IsEnabled.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task StartAsync_EnvironmentVariableOverride_EnablesRecoveryMode()
    {
        // Set environment variable
        Environment.SetEnvironmentVariable("NOCTURNE_RECOVERY_MODE", "true");
        try
        {
            var service = CreateService();
            await service.StartAsync(CancellationToken.None);

            _state.IsEnabled.Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOCTURNE_RECOVERY_MODE", null);
        }
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.API.Services.Auth;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

/// <summary>
/// Unit tests for TotpService covering setup, verification, and credential management.
/// </summary>
public class TotpServiceTests
{
    private readonly NocturneDbContext _dbContext;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly Guid _subjectId = Guid.CreateVersion7();
    private const string TestUsername = "testuser";

    public TotpServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryContext();
        _dataProtectionProvider = new EphemeralDataProtectionProvider();
    }

    #region GenerateSetupAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateSetupAsync_ReturnsProvisioningUri()
    {
        var service = CreateService();

        var result = await service.GenerateSetupAsync(_subjectId, TestUsername);

        result.ProvisioningUri.Should().StartWith("otpauth://totp/Nocturne:");
        result.ProvisioningUri.Should().Contain(TestUsername);
        result.ProvisioningUri.Should().Contain("secret=");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateSetupAsync_ReturnsBase32Secret()
    {
        var service = CreateService();

        var result = await service.GenerateSetupAsync(_subjectId, TestUsername);

        result.Base32Secret.Should().NotBeNullOrWhiteSpace();
        // Base32 chars only
        result.Base32Secret.Should().MatchRegex("^[A-Z2-7]+$");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateSetupAsync_ReturnsChallengeToken()
    {
        var service = CreateService();

        var result = await service.GenerateSetupAsync(_subjectId, TestUsername);

        result.ChallengeToken.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region CompleteSetupAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompleteSetupAsync_WithValidCode_PersistsCredential()
    {
        var service = CreateService();
        var setup = await service.GenerateSetupAsync(_subjectId, TestUsername);

        // Generate a valid TOTP code from the secret
        var secret = TotpHelper.GenerateSecret();
        // We need to use the actual secret from the setup, so we'll compute the code
        // from the base32 secret. Instead, use the challenge token flow end-to-end.
        var code = GenerateValidCode(setup.Base32Secret);

        var result = await service.CompleteSetupAsync(code, "My Authenticator", setup.ChallengeToken);

        result.SubjectId.Should().Be(_subjectId);
        result.CredentialId.Should().NotBeEmpty();

        var entity = await _dbContext.TotpCredentials.FirstOrDefaultAsync(c => c.Id == result.CredentialId);
        entity.Should().NotBeNull();
        entity!.SubjectId.Should().Be(_subjectId);
        entity.Label.Should().Be("My Authenticator");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompleteSetupAsync_WithInvalidCode_Throws()
    {
        var service = CreateService();
        var setup = await service.GenerateSetupAsync(_subjectId, TestUsername);

        var act = () => service.CompleteSetupAsync("000000", "Label", setup.ChallengeToken);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid TOTP code*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CompleteSetupAsync_WithTamperedToken_Throws()
    {
        var service = CreateService();

        var act = () => service.CompleteSetupAsync("123456", "Label", "tampered-token");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid or tampered*");
    }

    #endregion

    #region VerifyLoginAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyLoginAsync_WithValidCredential_ReturnsSubject()
    {
        var service = CreateService();

        // Seed subject and credential
        var secret = TotpHelper.GenerateSecret();
        var subject = SeedSubject(TestUsername);
        SeedCredential(subject.Id, secret, "Test TOTP");

        var code = TotpHelper.ComputeTotp(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var result = await service.VerifyLoginAsync(TestUsername, code);

        result.Should().NotBeNull();
        result!.SubjectId.Should().Be(subject.Id);
        result.Username.Should().Be(TestUsername);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyLoginAsync_WithUnknownUsername_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.VerifyLoginAsync("nonexistent", "123456");

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyLoginAsync_WithWrongCode_ReturnsNull()
    {
        var service = CreateService();

        var secret = TotpHelper.GenerateSecret();
        var subject = SeedSubject(TestUsername);
        SeedCredential(subject.Id, secret, "Test TOTP");

        var result = await service.VerifyLoginAsync(TestUsername, "000000");

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyLoginAsync_UpdatesLastUsedAt()
    {
        var service = CreateService();

        var secret = TotpHelper.GenerateSecret();
        var subject = SeedSubject(TestUsername);
        var credential = SeedCredential(subject.Id, secret, "Test TOTP");

        credential.LastUsedAt.Should().BeNull();

        var code = TotpHelper.ComputeTotp(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await service.VerifyLoginAsync(TestUsername, code);

        var updated = await _dbContext.TotpCredentials.FirstAsync(c => c.Id == credential.Id);
        updated.LastUsedAt.Should().NotBeNull();
    }

    #endregion

    #region GetCredentialsAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCredentialsAsync_ReturnsRegisteredCredentials()
    {
        var service = CreateService();

        SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "App 1");
        SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "App 2");
        // Another subject's credential - should not be returned
        SeedCredential(Guid.CreateVersion7(), TotpHelper.GenerateSecret(), "Other");

        var credentials = await service.GetCredentialsAsync(_subjectId);

        credentials.Should().HaveCount(2);
        credentials.Select(c => c.Label).Should().BeEquivalentTo(["App 1", "App 2"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCredentialsAsync_DoesNotExposeSecret()
    {
        var service = CreateService();
        SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "My App");

        var credentials = await service.GetCredentialsAsync(_subjectId);

        // TotpCredentialInfo record has no Secret property
        credentials.Should().HaveCount(1);
        var info = credentials[0];
        info.Id.Should().NotBeEmpty();
        info.Label.Should().Be("My App");
    }

    #endregion

    #region RemoveCredentialAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RemoveCredentialAsync_DeletesFromDb()
    {
        var service = CreateService();
        var credential = SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "To Delete");

        await service.RemoveCredentialAsync(credential.Id, _subjectId);

        var exists = await _dbContext.TotpCredentials.AnyAsync(c => c.Id == credential.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RemoveCredentialAsync_WrongSubject_Throws()
    {
        var service = CreateService();
        var credential = SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "Credential");

        var act = () => service.RemoveCredentialAsync(credential.Id, Guid.CreateVersion7());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task RemoveCredentialAsync_NonexistentId_Throws()
    {
        var service = CreateService();

        var act = () => service.RemoveCredentialAsync(Guid.CreateVersion7(), _subjectId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    #endregion

    #region GetCredentialCountAsync

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCredentialCountAsync_ReturnsCorrectCount()
    {
        var service = CreateService();

        SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "A");
        SeedCredential(_subjectId, TotpHelper.GenerateSecret(), "B");

        var count = await service.GetCredentialCountAsync(_subjectId);

        count.Should().Be(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetCredentialCountAsync_NoCredentials_ReturnsZero()
    {
        var service = CreateService();

        var count = await service.GetCredentialCountAsync(_subjectId);

        count.Should().Be(0);
    }

    #endregion

    #region Helpers

    private TotpService CreateService()
    {
        return new TotpService(_dbContext, _dataProtectionProvider, NullLogger<TotpService>.Instance);
    }

    private SubjectEntity SeedSubject(string username)
    {
        var subject = new SubjectEntity
        {
            Id = Guid.CreateVersion7(),
            Name = username,
            Username = username,
            IsActive = true,
        };

        _dbContext.Subjects.Add(subject);
        _dbContext.SaveChanges();
        return subject;
    }

    private TotpCredentialEntity SeedCredential(Guid subjectId, byte[] secret, string label)
    {
        var entity = new TotpCredentialEntity
        {
            Id = Guid.CreateVersion7(),
            SubjectId = subjectId,
            SecretKey = secret,
            Label = label,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.TotpCredentials.Add(entity);
        _dbContext.SaveChanges();
        return entity;
    }

    /// <summary>
    /// Decodes a base32 secret and computes a valid TOTP code for the current time.
    /// </summary>
    private static string GenerateValidCode(string base32Secret)
    {
        var secret = FromBase32(base32Secret);
        return TotpHelper.ComputeTotp(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>
    /// Decodes RFC 4648 base32 (no padding) back to bytes.
    /// </summary>
    private static byte[] FromBase32(string base32)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;

        foreach (var c in base32.ToUpperInvariant())
        {
            var val = alphabet.IndexOf(c);
            if (val < 0) continue;

            buffer = (buffer << 5) | val;
            bitsLeft += 5;

            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)(buffer >> bitsLeft));
            }
        }

        return output.ToArray();
    }

    #endregion
}

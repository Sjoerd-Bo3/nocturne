using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Tests.Shared.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

/// <summary>
/// Unit tests for RecoveryCodeService
/// </summary>
public class RecoveryCodeServiceTests
{
    private readonly NocturneDbContext _dbContext;
    private readonly RecoveryCodeService _service;
    private readonly Guid _subjectId = Guid.CreateVersion7();

    public RecoveryCodeServiceTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryContext();

        var jwtOptions = Options.Create(
            new JwtOptions
            {
                SecretKey = "this-is-a-test-secret-key-that-is-at-least-32-chars-long",
                Issuer = "test",
                Audience = "test",
            }
        );

        _service = new RecoveryCodeService(_dbContext, jwtOptions);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateCodesAsync_Returns8Codes()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        codes.Should().HaveCount(8);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateCodesAsync_CodesMatchExpectedFormat()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        foreach (var code in codes)
        {
            code.Should().MatchRegex(@"^[A-Z2-9]{5}-[A-Z2-9]{5}$");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateCodesAsync_CodesStoredAsHashesInDb()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        var entities = await _dbContext.RecoveryCodes
            .Where(r => r.SubjectId == _subjectId)
            .ToListAsync();

        entities.Should().HaveCount(8);

        // Hashes should not match any plaintext code
        var plainCodes = codes.Select(c => c.Replace("-", "")).ToHashSet();
        foreach (var entity in entities)
        {
            plainCodes.Should().NotContain(entity.CodeHash);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateCodesAsync_RegenerationInvalidatesExistingCodes()
    {
        await _service.GenerateCodesAsync(_subjectId);
        await _service.GenerateCodesAsync(_subjectId);

        var entities = await _dbContext.RecoveryCodes
            .Where(r => r.SubjectId == _subjectId)
            .ToListAsync();

        entities.Should().HaveCount(8, "regeneration should delete old codes and create exactly 8 new ones");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAndConsumeAsync_ValidCode_ReturnsTrue()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        var result = await _service.VerifyAndConsumeAsync(_subjectId, codes[0]);

        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAndConsumeAsync_CaseInsensitive()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);
        var lowerCode = codes[0].ToLowerInvariant();

        var result = await _service.VerifyAndConsumeAsync(_subjectId, lowerCode);

        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAndConsumeAsync_UsedCode_ReturnsFalse()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        await _service.VerifyAndConsumeAsync(_subjectId, codes[0]);
        var result = await _service.VerifyAndConsumeAsync(_subjectId, codes[0]);

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task VerifyAndConsumeAsync_InvalidCode_ReturnsFalse()
    {
        await _service.GenerateCodesAsync(_subjectId);

        var result = await _service.VerifyAndConsumeAsync(_subjectId, "AAAAA-BBBBB");

        result.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetRemainingCountAsync_ReturnsCorrectCountAfterConsumption()
    {
        var codes = await _service.GenerateCodesAsync(_subjectId);

        var initialCount = await _service.GetRemainingCountAsync(_subjectId);
        initialCount.Should().Be(8);

        await _service.VerifyAndConsumeAsync(_subjectId, codes[0]);
        await _service.VerifyAndConsumeAsync(_subjectId, codes[1]);

        var remaining = await _service.GetRemainingCountAsync(_subjectId);
        remaining.Should().Be(6);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasCodesAsync_WithCodes_ReturnsTrue()
    {
        await _service.GenerateCodesAsync(_subjectId);

        var result = await _service.HasCodesAsync(_subjectId);

        result.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task HasCodesAsync_WithoutCodes_ReturnsFalse()
    {
        var result = await _service.HasCodesAsync(_subjectId);

        result.Should().BeFalse();
    }
}

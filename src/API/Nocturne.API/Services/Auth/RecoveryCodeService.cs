using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Service for generating and verifying single-use recovery codes for break-glass account access
/// </summary>
public class RecoveryCodeService : IRecoveryCodeService
{
    private const int CodeCount = 8;
    private const int SegmentLength = 5;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly NocturneDbContext _dbContext;
    private readonly byte[] _hmacKey;

    /// <summary>
    /// Creates a new instance of RecoveryCodeService
    /// </summary>
    public RecoveryCodeService(NocturneDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _hmacKey = Encoding.UTF8.GetBytes(jwtOptions.Value.SecretKey);
    }

    /// <inheritdoc />
    public async Task<List<string>> GenerateCodesAsync(Guid subjectId)
    {
        // Delete all existing codes for this subject
        var existing = await _dbContext.RecoveryCodes
            .Where(r => r.SubjectId == subjectId)
            .ToListAsync();

        if (existing.Count > 0)
        {
            _dbContext.RecoveryCodes.RemoveRange(existing);
        }

        var codes = new List<string>(CodeCount);

        for (var i = 0; i < CodeCount; i++)
        {
            var code = GenerateCode();
            codes.Add(code);

            var hash = ComputeHmac(NormalizeCode(code));

            _dbContext.RecoveryCodes.Add(new RecoveryCodeEntity
            {
                Id = Guid.CreateVersion7(),
                SubjectId = subjectId,
                CodeHash = hash,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await _dbContext.SaveChangesAsync();

        return codes;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyAndConsumeAsync(Guid subjectId, string code)
    {
        var normalized = NormalizeCode(code);
        var hash = ComputeHmac(normalized);

        var entity = await _dbContext.RecoveryCodes
            .Where(r => r.SubjectId == subjectId && r.CodeHash == hash && r.UsedAt == null)
            .FirstOrDefaultAsync();

        if (entity is null)
        {
            return false;
        }

        entity.UsedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    /// <inheritdoc />
    public async Task<int> GetRemainingCountAsync(Guid subjectId)
    {
        return await _dbContext.RecoveryCodes
            .CountAsync(r => r.SubjectId == subjectId && r.UsedAt == null);
    }

    /// <inheritdoc />
    public async Task<bool> HasCodesAsync(Guid subjectId)
    {
        return await _dbContext.RecoveryCodes
            .AnyAsync(r => r.SubjectId == subjectId);
    }

    private static string GenerateCode()
    {
        var segment1 = GenerateSegment();
        var segment2 = GenerateSegment();
        return $"{segment1}-{segment2}";
    }

    private static string GenerateSegment()
    {
        var chars = new char[SegmentLength];
        for (var i = 0; i < SegmentLength; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }

    private static string NormalizeCode(string code)
    {
        return code.ToUpperInvariant().Replace("-", "");
    }

    private string ComputeHmac(string normalizedCode)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedCode));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Generates and verifies single-use recovery codes for break-glass account access.
/// Each code consists of two 5-character segments from a reduced unambiguous alphabet,
/// separated by a hyphen. Codes are stored as HMAC-SHA256 hashes keyed on the JWT secret.
/// </summary>
/// <seealso cref="IRecoveryCodeService"/>
public class RecoveryCodeService : IRecoveryCodeService
{
    private const int CodeCount = 8;
    private const int SegmentLength = 5;
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    private readonly NocturneDbContext _dbContext;
    private readonly byte[] _hmacKey;

    /// <summary>
    /// Initialises a new <see cref="RecoveryCodeService"/>.
    /// </summary>
    /// <param name="dbContext">Database context for reading and writing recovery code records.</param>
    /// <param name="jwtOptions">JWT options whose <c>SecretKey</c> is used as the HMAC key for hashing codes.</param>
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

    /// <summary>
    /// Generates a single recovery code in <c>XXXXX-XXXXX</c> format using <see cref="Alphabet"/>.
    /// </summary>
    /// <returns>A formatted recovery code string.</returns>
    private static string GenerateCode()
    {
        var segment1 = GenerateSegment();
        var segment2 = GenerateSegment();
        return $"{segment1}-{segment2}";
    }

    /// <summary>
    /// Generates a single segment of <see cref="SegmentLength"/> characters from <see cref="Alphabet"/>
    /// using a cryptographically secure RNG.
    /// </summary>
    /// <returns>A random character segment string.</returns>
    private static string GenerateSegment()
    {
        var chars = new char[SegmentLength];
        for (var i = 0; i < SegmentLength; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }
        return new string(chars);
    }

    /// <summary>
    /// Normalises a code by converting to uppercase and stripping hyphens,
    /// ensuring consistent database lookup regardless of user input formatting.
    /// </summary>
    /// <param name="code">The raw recovery code string entered by the user.</param>
    /// <returns>The normalised form suitable for hashing and database comparison.</returns>
    private static string NormalizeCode(string code)
    {
        return code.ToUpperInvariant().Replace("-", "");
    }

    /// <summary>
    /// Computes an HMAC-SHA256 hash of the normalised code using the configured JWT secret key.
    /// </summary>
    /// <param name="normalizedCode">The normalised (uppercase, hyphen-free) code to hash.</param>
    /// <returns>A lowercase hexadecimal hash string suitable for database storage.</returns>
    private string ComputeHmac(string normalizedCode)
    {
        using var hmac = new HMACSHA256(_hmacKey);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedCode));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nocturne.API.Extensions;
using Nocturne.API.Models;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using SameSiteMode = Nocturne.Core.Models.Configuration.SameSiteMode;

namespace Nocturne.API.Controllers;

/// <summary>
/// Controller for WebAuthn/FIDO2 passkey authentication ceremonies.
/// Handles registration, login (both discoverable and non-discoverable), and recovery code verification.
/// </summary>
[ApiController]
[Route("api/auth/passkey")]
[Tags("Passkey")]
public class PasskeyController : ControllerBase
{
    private const string ChallengeCookieName = ".Nocturne.PasskeyChallenge";
    private const string RecoveryCookieName = ".Nocturne.RecoverySession";

    private readonly IPasskeyService _passkeyService;
    private readonly IRecoveryCodeService _recoveryCodeService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ISubjectService _subjectService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly NocturneDbContext _dbContext;
    private readonly OidcOptions _oidcOptions;
    private readonly ILogger<PasskeyController> _logger;

    /// <summary>
    /// Creates a new instance of PasskeyController
    /// </summary>
    public PasskeyController(
        IPasskeyService passkeyService,
        IRecoveryCodeService recoveryCodeService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ISubjectService subjectService,
        ITenantAccessor tenantAccessor,
        NocturneDbContext dbContext,
        IOptions<OidcOptions> oidcOptions,
        ILogger<PasskeyController> logger)
    {
        _passkeyService = passkeyService;
        _recoveryCodeService = recoveryCodeService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _subjectService = subjectService;
        _tenantAccessor = tenantAccessor;
        _dbContext = dbContext;
        _oidcOptions = oidcOptions.Value;
        _logger = logger;
    }

    /// <summary>
    /// Generate registration options for a new passkey credential
    /// </summary>
    [HttpPost("register/options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegisterOptions([FromBody] PasskeyRegisterOptionsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new ErrorResponse { Error = "invalid_request", Message = "Username is required" });
        }

        var tenantId = _tenantAccessor.TenantId;
        var result = await _passkeyService.GenerateRegistrationOptionsAsync(
            request.SubjectId, request.Username, tenantId);

        SetChallengeCookie(result.ChallengeCookie);

        return Content(result.OptionsJson, "application/json");
    }

    /// <summary>
    /// Complete passkey registration with attestation response
    /// </summary>
    [HttpPost("register/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyRegisterCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyRegisterCompleteResponse>> RegisterComplete(
        [FromBody] PasskeyRegisterCompleteRequest request)
    {
        var challengeCookie = Request.Cookies[ChallengeCookieName];
        if (string.IsNullOrEmpty(challengeCookie))
        {
            return BadRequest(new ErrorResponse { Error = "invalid_state", Message = "Challenge cookie not found or expired" });
        }

        var tenantId = _tenantAccessor.TenantId;

        try
        {
            var result = await _passkeyService.CompleteRegistrationAsync(
                request.AttestationResponseJson, challengeCookie, tenantId);

            ClearChallengeCookie();

            return Ok(new PasskeyRegisterCompleteResponse
            {
                CredentialId = result.CredentialId,
                SubjectId = result.SubjectId,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey registration completion failed");
            return BadRequest(new ErrorResponse { Error = "registration_failed", Message = "Passkey registration failed" });
        }
    }

    /// <summary>
    /// Generate discoverable assertion options (no username required)
    /// </summary>
    [HttpPost("login/discoverable/options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DiscoverableLoginOptions()
    {
        var tenantId = _tenantAccessor.TenantId;
        var result = await _passkeyService.GenerateDiscoverableAssertionOptionsAsync(tenantId);

        SetChallengeCookie(result.ChallengeCookie);

        return Content(result.OptionsJson, "application/json");
    }

    /// <summary>
    /// Generate assertion options for a specific user
    /// </summary>
    [HttpPost("login/options")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LoginOptions([FromBody] PasskeyLoginOptionsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new ErrorResponse { Error = "invalid_request", Message = "Username is required" });
        }

        var tenantId = _tenantAccessor.TenantId;
        var result = await _passkeyService.GenerateAssertionOptionsAsync(request.Username, tenantId);

        SetChallengeCookie(result.ChallengeCookie);

        return Content(result.OptionsJson, "application/json");
    }

    /// <summary>
    /// Complete passkey login with assertion response
    /// </summary>
    [HttpPost("login/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyLoginCompleteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PasskeyLoginCompleteResponse>> LoginComplete(
        [FromBody] PasskeyLoginCompleteRequest request)
    {
        var challengeCookie = Request.Cookies[ChallengeCookieName];
        if (string.IsNullOrEmpty(challengeCookie))
        {
            return BadRequest(new ErrorResponse { Error = "invalid_state", Message = "Challenge cookie not found or expired" });
        }

        var tenantId = _tenantAccessor.TenantId;

        try
        {
            var assertionResult = await _passkeyService.CompleteAssertionAsync(
                request.AssertionResponseJson, challengeCookie, tenantId);

            ClearChallengeCookie();

            // Get subject details for token generation
            var subject = await _subjectService.GetSubjectByIdAsync(assertionResult.SubjectId);
            if (subject == null)
            {
                return BadRequest(new ErrorResponse { Error = "subject_not_found", Message = "User account not found" });
            }

            var roles = await _subjectService.GetSubjectRolesAsync(assertionResult.SubjectId);
            var permissions = await _subjectService.GetSubjectPermissionsAsync(assertionResult.SubjectId);

            // Generate tokens
            var subjectInfo = new SubjectInfo
            {
                Id = subject.Id,
                Name = assertionResult.DisplayName ?? assertionResult.Username,
                Email = subject.Email,
                OidcSubjectId = subject.OidcSubjectId,
                OidcIssuer = subject.OidcIssuer,
            };

            var accessToken = _jwtService.GenerateAccessToken(subjectInfo, permissions, roles);
            var refreshToken = await _refreshTokenService.CreateRefreshTokenAsync(
                assertionResult.SubjectId,
                oidcSessionId: null,
                deviceDescription: "Passkey",
                ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString(),
                userAgent: Request.Headers.UserAgent.ToString());

            SetSessionCookies(accessToken, refreshToken);

            return Ok(new PasskeyLoginCompleteResponse
            {
                Success = true,
                AccessToken = accessToken,
                ExpiresIn = (int)_jwtService.GetAccessTokenLifetime().TotalSeconds,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey login completion failed");
            return BadRequest(new ErrorResponse { Error = "login_failed", Message = "Passkey authentication failed" });
        }
    }

    /// <summary>
    /// Verify a recovery code and issue a restricted recovery session
    /// </summary>
    [HttpPost("recovery/verify")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(RecoveryVerifyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RecoveryVerifyResponse>> RecoveryVerify(
        [FromBody] RecoveryVerifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new ErrorResponse { Error = "invalid_request", Message = "Username and recovery code are required" });
        }

        var tenantId = _tenantAccessor.TenantId;

        // Look up subject by username within the current tenant
        var subjectEntity = await _dbContext.TenantMembers
            .AsNoTracking()
            .Where(tm => tm.TenantId == tenantId)
            .Select(tm => tm.Subject)
            .FirstOrDefaultAsync(s => s != null && s.Username == request.Username);

        if (subjectEntity == null)
        {
            // Don't reveal whether the username exists
            return BadRequest(new ErrorResponse { Error = "recovery_failed", Message = "Invalid username or recovery code" });
        }

        var verified = await _recoveryCodeService.VerifyAndConsumeAsync(subjectEntity.Id, request.Code);
        if (!verified)
        {
            return BadRequest(new ErrorResponse { Error = "recovery_failed", Message = "Invalid username or recovery code" });
        }

        // Issue a restricted recovery session (short-lived)
        var subjectInfo = new SubjectInfo
        {
            Id = subjectEntity.Id,
            Name = subjectEntity.Name,
            Email = subjectEntity.Email,
            OidcSubjectId = subjectEntity.OidcSubjectId,
            OidcIssuer = subjectEntity.OidcIssuer,
        };

        var recoveryToken = _jwtService.GenerateAccessToken(
            subjectInfo,
            permissions: ["passkey:manage"],
            roles: [],
            lifetime: TimeSpan.FromMinutes(10));

        Response.Cookies.Append(RecoveryCookieName, recoveryToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/",
            IsEssential = true,
        });

        return Ok(new RecoveryVerifyResponse
        {
            Success = true,
            RemainingCodes = await _recoveryCodeService.GetRemainingCountAsync(subjectEntity.Id),
        });
    }

    /// <summary>
    /// List all passkey credentials for the authenticated user
    /// </summary>
    [HttpGet("credentials")]
    [ProducesResponseType(typeof(PasskeyCredentialListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PasskeyCredentialListResponse>> ListCredentials()
    {
        var auth = HttpContext.GetAuthContext();
        if (auth == null || !auth.IsAuthenticated || auth.SubjectId == null)
        {
            return Unauthorized(new ErrorResponse { Error = "unauthorized", Message = "Authentication required" });
        }

        var tenantId = _tenantAccessor.TenantId;
        var credentials = await _passkeyService.GetCredentialsAsync(auth.SubjectId.Value, tenantId);
        var hasOidc = await _passkeyService.HasOidcLinkAsync(auth.SubjectId.Value);

        return Ok(new PasskeyCredentialListResponse
        {
            Credentials = credentials.Select(c => new PasskeyCredentialDto
            {
                Id = c.Id,
                Label = c.Label,
                CreatedAt = c.CreatedAt,
                LastUsedAt = c.LastUsedAt,
            }).ToList(),
            HasOidcLink = hasOidc,
        });
    }

    /// <summary>
    /// Remove a passkey credential. Cannot remove the last credential if user has no OIDC link.
    /// </summary>
    [HttpDelete("credentials/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveCredential(Guid id)
    {
        var auth = HttpContext.GetAuthContext();
        if (auth == null || !auth.IsAuthenticated || auth.SubjectId == null)
        {
            return Unauthorized(new ErrorResponse { Error = "unauthorized", Message = "Authentication required" });
        }

        var tenantId = _tenantAccessor.TenantId;

        // Check removal protection: cannot remove last passkey if no OIDC link
        var credentialCount = await _passkeyService.GetCredentialCountAsync(auth.SubjectId.Value, tenantId);
        var hasOidc = await _passkeyService.HasOidcLinkAsync(auth.SubjectId.Value);

        if (credentialCount <= 1 && !hasOidc)
        {
            return BadRequest(new ErrorResponse
            {
                Error = "removal_blocked",
                Message = "Cannot remove your last passkey without an alternative sign-in method",
            });
        }

        try
        {
            await _passkeyService.RemoveCredentialAsync(id, auth.SubjectId.Value, tenantId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove passkey credential {CredentialId}", id);
            return NotFound(new ErrorResponse { Error = "not_found", Message = "Credential not found" });
        }
    }

    /// <summary>
    /// Regenerate recovery codes for the authenticated user. Invalidates all existing codes.
    /// </summary>
    [HttpPost("recovery/regenerate")]
    [ProducesResponseType(typeof(RecoveryRegenerateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecoveryRegenerateResponse>> RegenerateRecoveryCodes()
    {
        var auth = HttpContext.GetAuthContext();
        if (auth == null || !auth.IsAuthenticated || auth.SubjectId == null)
        {
            return Unauthorized(new ErrorResponse { Error = "unauthorized", Message = "Authentication required" });
        }

        var codes = await _recoveryCodeService.GenerateCodesAsync(auth.SubjectId.Value);

        return Ok(new RecoveryRegenerateResponse
        {
            Codes = codes,
        });
    }

    /// <summary>
    /// Get the count of remaining recovery codes for the authenticated user
    /// </summary>
    [HttpGet("recovery/status")]
    [ProducesResponseType(typeof(RecoveryStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<RecoveryStatusResponse>> GetRecoveryStatus()
    {
        var auth = HttpContext.GetAuthContext();
        if (auth == null || !auth.IsAuthenticated || auth.SubjectId == null)
        {
            return Unauthorized(new ErrorResponse { Error = "unauthorized", Message = "Authentication required" });
        }

        var remaining = await _recoveryCodeService.GetRemainingCountAsync(auth.SubjectId.Value);
        var hasCodes = await _recoveryCodeService.HasCodesAsync(auth.SubjectId.Value);

        return Ok(new RecoveryStatusResponse
        {
            RemainingCodes = remaining,
            HasCodes = hasCodes,
            TotalCodes = 8,
        });
    }

    #region Private Helpers

    private void SetChallengeCookie(string challengeData)
    {
        Response.Cookies.Append(ChallengeCookieName, challengeData, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(5),
            Path = "/api/auth/passkey",
            IsEssential = true,
        });
    }

    private void ClearChallengeCookie()
    {
        Response.Cookies.Delete(ChallengeCookieName, new CookieOptions
        {
            Path = "/api/auth/passkey",
        });
    }

    private void SetSessionCookies(string accessToken, string refreshToken)
    {
        var cookieSameSite = _oidcOptions.Cookie.SameSite switch
        {
            SameSiteMode.Strict => Microsoft.AspNetCore.Http.SameSiteMode.Strict,
            SameSiteMode.Lax => Microsoft.AspNetCore.Http.SameSiteMode.Lax,
            SameSiteMode.None => Microsoft.AspNetCore.Http.SameSiteMode.None,
            _ => Microsoft.AspNetCore.Http.SameSiteMode.Lax,
        };

        Response.Cookies.Append(_oidcOptions.Cookie.AccessTokenName, accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = _oidcOptions.Cookie.Secure,
            SameSite = cookieSameSite,
            Path = "/",
            IsEssential = true,
            MaxAge = _jwtService.GetAccessTokenLifetime(),
        });

        Response.Cookies.Append(_oidcOptions.Cookie.RefreshTokenName, refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = _oidcOptions.Cookie.Secure,
            SameSite = cookieSameSite,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(7),
        });

        Response.Cookies.Append("IsAuthenticated", "true", new CookieOptions
        {
            HttpOnly = false,
            Secure = _oidcOptions.Cookie.Secure,
            SameSite = cookieSameSite,
            Path = "/",
            MaxAge = TimeSpan.FromDays(7),
        });
    }

    #endregion
}

#region Request/Response DTOs

/// <summary>
/// Request for passkey registration options
/// </summary>
public class PasskeyRegisterOptionsRequest
{
    public Guid SubjectId { get; set; }
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Request to complete passkey registration
/// </summary>
public class PasskeyRegisterCompleteRequest
{
    public string AttestationResponseJson { get; set; } = string.Empty;
    public string? Label { get; set; }
}

/// <summary>
/// Response for completed passkey registration
/// </summary>
public class PasskeyRegisterCompleteResponse
{
    public Guid CredentialId { get; set; }
    public Guid SubjectId { get; set; }
}

/// <summary>
/// Request for passkey login options
/// </summary>
public class PasskeyLoginOptionsRequest
{
    public string Username { get; set; } = string.Empty;
}

/// <summary>
/// Request to complete passkey login
/// </summary>
public class PasskeyLoginCompleteRequest
{
    public string AssertionResponseJson { get; set; } = string.Empty;
}

/// <summary>
/// Response for completed passkey login
/// </summary>
public class PasskeyLoginCompleteResponse
{
    public bool Success { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresIn { get; set; }
}

/// <summary>
/// Request to verify a recovery code
/// </summary>
public class RecoveryVerifyRequest
{
    public string Username { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

/// <summary>
/// Response for recovery code verification
/// </summary>
public class RecoveryVerifyResponse
{
    public bool Success { get; set; }
    public int RemainingCodes { get; set; }
}

/// <summary>
/// Response containing the list of passkey credentials
/// </summary>
public class PasskeyCredentialListResponse
{
    public List<PasskeyCredentialDto> Credentials { get; set; } = new();
    public bool HasOidcLink { get; set; }
}

/// <summary>
/// A passkey credential summary (never includes the public key)
/// </summary>
public class PasskeyCredentialDto
{
    public Guid Id { get; set; }
    public string? Label { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}

/// <summary>
/// Response containing regenerated recovery codes
/// </summary>
public class RecoveryRegenerateResponse
{
    public List<string> Codes { get; set; } = new();
}

/// <summary>
/// Response containing recovery code status
/// </summary>
public class RecoveryStatusResponse
{
    public int RemainingCodes { get; set; }
    public bool HasCodes { get; set; }
    public int TotalCodes { get; set; }
}

#endregion

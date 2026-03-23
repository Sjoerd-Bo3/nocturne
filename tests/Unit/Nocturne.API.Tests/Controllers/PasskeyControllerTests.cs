using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Controllers;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Data;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

public class PasskeyControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<IPasskeyService> _passkeyService;
    private readonly Mock<IRecoveryCodeService> _recoveryCodeService;
    private readonly Mock<IJwtService> _jwtService;
    private readonly Mock<IRefreshTokenService> _refreshTokenService;
    private readonly Mock<ISubjectService> _subjectService;
    private readonly Mock<ITenantAccessor> _tenantAccessor;
    private readonly PasskeyController _controller;

    private readonly Guid _tenantId = Guid.CreateVersion7();

    public PasskeyControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(_dbOptions);
        _dbContext.Database.EnsureCreated();

        _passkeyService = new Mock<IPasskeyService>();
        _recoveryCodeService = new Mock<IRecoveryCodeService>();
        _jwtService = new Mock<IJwtService>();
        _refreshTokenService = new Mock<IRefreshTokenService>();
        _subjectService = new Mock<ISubjectService>();
        _tenantAccessor = new Mock<ITenantAccessor>();
        _tenantAccessor.Setup(t => t.TenantId).Returns(_tenantId);

        var oidcOptions = Options.Create(new OidcOptions
        {
            Cookie = new CookieSettings
            {
                AccessTokenName = ".Nocturne.AccessToken",
                RefreshTokenName = ".Nocturne.RefreshToken",
                Secure = true,
            },
        });

        var logger = new Mock<ILogger<PasskeyController>>();

        _controller = new PasskeyController(
            _passkeyService.Object,
            _recoveryCodeService.Object,
            _jwtService.Object,
            _refreshTokenService.Object,
            _subjectService.Object,
            _tenantAccessor.Object,
            _dbContext,
            oidcOptions,
            logger.Object);

        // Set up HttpContext with response cookies
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterOptions_EmptyUsername_ReturnsBadRequest()
    {
        var request = new PasskeyRegisterOptionsRequest
        {
            SubjectId = Guid.CreateVersion7(),
            Username = "",
        };

        var result = await _controller.RegisterOptions(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task RegisterOptions_ValidRequest_CallsServiceAndReturnsJson()
    {
        var subjectId = Guid.CreateVersion7();
        _passkeyService
            .Setup(s => s.GenerateRegistrationOptionsAsync(subjectId, "testuser", _tenantId))
            .ReturnsAsync(new PasskeyRegistrationOptions("{\"challenge\":\"abc\"}", "cookie-data"));

        var request = new PasskeyRegisterOptionsRequest
        {
            SubjectId = subjectId,
            Username = "testuser",
        };

        var result = await _controller.RegisterOptions(request);

        Assert.IsType<ContentResult>(result);
        var content = (ContentResult)result;
        Assert.Equal("application/json", content.ContentType);
        Assert.Contains("challenge", content.Content);
        _passkeyService.Verify(s => s.GenerateRegistrationOptionsAsync(subjectId, "testuser", _tenantId), Times.Once);
    }

    [Fact]
    public async Task RegisterComplete_NoCookie_ReturnsBadRequest()
    {
        var request = new PasskeyRegisterCompleteRequest
        {
            AttestationResponseJson = "{}",
        };

        var result = await _controller.RegisterComplete(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginOptions_EmptyUsername_ReturnsBadRequest()
    {
        var request = new PasskeyLoginOptionsRequest { Username = "" };

        var result = await _controller.LoginOptions(request);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task LoginOptions_ValidRequest_CallsService()
    {
        _passkeyService
            .Setup(s => s.GenerateAssertionOptionsAsync("testuser", _tenantId))
            .ReturnsAsync(new PasskeyAssertionOptions("{\"challenge\":\"xyz\"}", "assertion-cookie"));

        var request = new PasskeyLoginOptionsRequest { Username = "testuser" };

        var result = await _controller.LoginOptions(request);

        Assert.IsType<ContentResult>(result);
        _passkeyService.Verify(s => s.GenerateAssertionOptionsAsync("testuser", _tenantId), Times.Once);
    }

    [Fact]
    public async Task DiscoverableLoginOptions_CallsService()
    {
        _passkeyService
            .Setup(s => s.GenerateDiscoverableAssertionOptionsAsync(_tenantId))
            .ReturnsAsync(new PasskeyAssertionOptions("{\"challenge\":\"disc\"}", "disc-cookie"));

        var result = await _controller.DiscoverableLoginOptions();

        Assert.IsType<ContentResult>(result);
        _passkeyService.Verify(s => s.GenerateDiscoverableAssertionOptionsAsync(_tenantId), Times.Once);
    }

    [Fact]
    public async Task LoginComplete_NoCookie_ReturnsBadRequest()
    {
        var request = new PasskeyLoginCompleteRequest { AssertionResponseJson = "{}" };

        var result = await _controller.LoginComplete(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RecoveryVerify_EmptyFields_ReturnsBadRequest()
    {
        var request = new RecoveryVerifyRequest { Username = "", Code = "" };

        var result = await _controller.RecoveryVerify(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RecoveryVerify_UnknownUser_ReturnsBadRequest()
    {
        var request = new RecoveryVerifyRequest { Username = "nonexistent", Code = "123456" };

        var result = await _controller.RecoveryVerify(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }
}

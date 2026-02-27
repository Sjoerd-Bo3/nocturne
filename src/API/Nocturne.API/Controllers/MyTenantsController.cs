using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Controllers;

[ApiController]
[Route("api/me/tenants")]
[Authorize]
public class MyTenantsController : ControllerBase
{
    private readonly ITenantService _tenantService;

    public MyTenantsController(ITenantService tenantService)
    {
        _tenantService = tenantService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyTenants(CancellationToken ct)
    {
        var authContext = HttpContext.Items["AuthContext"] as AuthContext;
        if (authContext?.SubjectId == null)
            return Unauthorized();

        var tenants = await _tenantService.GetTenantsForSubjectAsync(authContext.SubjectId.Value, ct);
        return Ok(tenants);
    }
}

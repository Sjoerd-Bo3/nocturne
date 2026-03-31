using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nocturne.API.Extensions;

namespace Nocturne.API.Filters;

/// <summary>
/// MVC authorization filter that enforces site-wide lockdown for controller endpoints.
/// Runs inside the MVC pipeline where endpoint metadata (including [AllowAnonymous])
/// is always resolved, unlike middleware which may execute before routing completes.
/// </summary>
public class SiteSecurityFilter : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        // Only enforce when the middleware flagged lockdown as active
        if (context.HttpContext.Items["SiteLockdownEnabled"] is not true)
            return;

        // Respect [AllowAnonymous] — controllers are the single source of truth
        if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
            return;

        // Check if user is authenticated
        var authContext = context.HttpContext.GetAuthContext();
        if (authContext != null && authContext.IsAuthenticated)
            return;

        context.Result = new JsonResult(new
        {
            error = "authentication_required",
            error_description = "This site requires authentication. Please log in to access this resource.",
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
        };
    }
}

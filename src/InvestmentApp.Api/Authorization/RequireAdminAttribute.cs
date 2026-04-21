using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace InvestmentApp.Api.Authorization;

/// <summary>
/// Requires the caller's JWT to include role=Admin AND NOT amr=impersonate.
/// Prevents an impersonation token from being used to start another impersonation (no nesting).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequireAdminAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var role = user.FindFirst("role")?.Value;
        if (!string.Equals(role, "Admin", StringComparison.Ordinal))
        {
            context.Result = new ForbidResult();
            return;
        }

        var amr = user.FindFirst("amr")?.Value;
        if (string.Equals(amr, "impersonate", StringComparison.Ordinal))
        {
            context.Result = new ObjectResult(new { error = "ADMIN_ACTION_BLOCKED_DURING_IMPERSONATION" })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }
    }
}

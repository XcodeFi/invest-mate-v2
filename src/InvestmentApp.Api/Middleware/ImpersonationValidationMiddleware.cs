using InvestmentApp.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace InvestmentApp.Api.Middleware;

/// <summary>
/// Validates impersonation tokens on every authenticated request.
/// - Revoked/ended sessions → 401 + X-Impersonation-Revoked: true
/// - Blocks mutation methods unless Admin:AllowImpersonateMutations is true
/// - Sets X-Impersonating: true response header
///
/// MUST run AFTER UseAuthentication (needs claims) and BEFORE UseAuthorization.
/// </summary>
public class ImpersonationValidationMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly HashSet<string> MutationMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "DELETE", "PATCH"
    };

    public ImpersonationValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        var impersonationId = context.User?.FindFirst("impersonation_id")?.Value;
        if (string.IsNullOrEmpty(impersonationId))
        {
            await _next(context);
            return;
        }

        // Resolve repository from scope-aware DI (middleware is singleton-per-app)
        var auditRepo = context.RequestServices.GetRequiredService<IImpersonationAuditRepository>();
        var audit = await auditRepo.GetByIdAsync(impersonationId, context.RequestAborted);

        if (audit == null || audit.IsRevoked || audit.EndedAt != null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers["X-Impersonation-Revoked"] = "true";
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "IMPERSONATION_SESSION_REVOKED"
            }));
            return;
        }

        context.Response.Headers["X-Impersonating"] = "true";

        // Allow the stop endpoint even when mutations are blocked
        var path = context.Request.Path.Value ?? string.Empty;
        var isStopEndpoint = path.EndsWith("/admin/impersonate/stop", StringComparison.OrdinalIgnoreCase);

        if (!isStopEndpoint && MutationMethods.Contains(context.Request.Method))
        {
            var allowMutations = configuration.GetValue<bool>("Admin:AllowImpersonateMutations", false);
            if (!allowMutations)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.Headers["X-Impersonation-Mutation-Blocked"] = "true";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "MUTATION_BLOCKED_DURING_IMPERSONATION"
                }));
                return;
            }
        }

        await _next(context);
    }
}

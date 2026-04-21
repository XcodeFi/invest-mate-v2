using InvestmentApp.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;

namespace InvestmentApp.Api.Middleware;

/// <summary>
/// Validates impersonation tokens on every Bearer-token request.
/// - Revoked/ended sessions → 401 + X-Impersonation-Revoked: true
/// - Blocks mutation methods unless Admin:AllowImpersonateMutations is true
/// - Sets X-Impersonating: true response header
///
/// Default auth scheme is Cookie, so UseAuthentication doesn't populate JWT
/// claims at this point — we authenticate the Bearer scheme explicitly.
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
        // Skip non-Bearer requests (cookie / OAuth callback flows).
        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Explicit JWT authentication — default auth scheme is Cookie, so JWT claims
        // aren't loaded into context.User until an [Authorize(JwtBearer)] endpoint fires.
        var jwtResult = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme);
        var principal = jwtResult.Succeeded ? jwtResult.Principal : null;
        var impersonationId = principal?.FindFirst("impersonation_id")?.Value;

        if (string.IsNullOrEmpty(impersonationId))
        {
            // Fallback: JWT validation may have failed (expired token or framework edge
            // cases). Peek at the raw token — if it's an impersonation token, surface
            // X-Impersonation-Revoked so the FE interceptor can auto-restore admin session.
            var rawToken = authHeader.Substring("Bearer ".Length).Trim();
            if (TryReadImpersonationIdWithoutValidation(rawToken, out var expiredId)
                && !string.IsNullOrEmpty(expiredId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["X-Impersonation-Revoked"] = "true";
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = "IMPERSONATION_SESSION_EXPIRED"
                }));
                return;
            }

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

    /// <summary>
    /// Decodes the JWT without validating signature/lifetime and returns the
    /// impersonation_id claim if present. Used to distinguish "malformed token"
    /// from "expired impersonation token" when full auth fails.
    /// </summary>
    public static bool TryReadImpersonationIdWithoutValidation(string rawToken, out string impersonationId)
    {
        impersonationId = string.Empty;
        if (string.IsNullOrWhiteSpace(rawToken)) return false;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
            var claim = jwt.Claims.FirstOrDefault(c => c.Type == "impersonation_id");
            if (claim != null && !string.IsNullOrEmpty(claim.Value))
            {
                impersonationId = claim.Value;
                return true;
            }
        }
        catch
        {
            // Malformed token — not our concern.
        }
        return false;
    }
}

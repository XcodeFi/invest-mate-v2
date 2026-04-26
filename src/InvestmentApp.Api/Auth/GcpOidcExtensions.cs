using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace InvestmentApp.Api.Auth;

/// <summary>
/// Registers a JWT bearer scheme that validates Google-issued OIDC ID tokens emitted by
/// Cloud Scheduler when it invokes a Cloud Run endpoint. Combined with the
/// <see cref="PolicyName"/> authorization policy, this ensures only allow-listed
/// service accounts can invoke <c>/internal/jobs/*</c> endpoints.
/// </summary>
public static class GcpOidcExtensions
{
    public const string SchemeName = "GcpOidc";
    public const string PolicyName = "GcpScheduler";

    public static AuthenticationBuilder AddGcpOidc(
        this AuthenticationBuilder builder,
        IConfiguration config)
    {
        var expectedAudience = config["Jobs:ExpectedAudience"];

        return builder.AddJwtBearer(SchemeName, options =>
        {
            // Authority triggers automatic JWKS fetch from Google's discovery doc.
            options.Authority = "https://accounts.google.com";
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                ValidateAudience = !string.IsNullOrEmpty(expectedAudience),
                ValidAudience = expectedAudience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        });
    }

    public static AuthorizationOptions AddGcpSchedulerPolicy(this AuthorizationOptions options)
    {
        options.AddPolicy(PolicyName, policy =>
        {
            policy.AuthenticationSchemes = new[] { SchemeName };
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
            {
                var allowlist = ctx.Resource is HttpContext httpCtx
                    ? httpCtx.RequestServices.GetService<SchedulerEmailAllowlist>()
                    : null;

                if (allowlist == null) return false;

                var email = ctx.User.FindFirst("email")?.Value
                         ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

                // email_verified must be true for Google-issued tokens to be trusted.
                var emailVerified = ctx.User.FindFirst("email_verified")?.Value;
                if (!string.Equals(emailVerified, "true", StringComparison.OrdinalIgnoreCase))
                    return false;

                return allowlist.IsAllowed(email);
            });
        });
        return options;
    }
}

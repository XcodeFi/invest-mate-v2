using System.Security.Claims;
using System.Text.Encodings.Web;
using InvestmentApp.Application.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace InvestmentApp.Api.Auth;

/// <summary>
/// Authenticates a per-user personal access token (see <see cref="InvestmentApp.Domain.Entities.ApiKey"/>)
/// presented in the <c>X-Api-Key</c> header. Resolves the token to its owning user and exposes a
/// <c>sub</c> claim so opt-in endpoints read the user the same way they do for the JWT scheme.
/// Registered as a scheme but never wired as a default — endpoints opt in explicitly via
/// <c>[Authorize(AuthenticationSchemes = ApiKeyAuthenticationDefaults.Scheme)]</c>.
/// </summary>
public static class ApiKeyAuthenticationDefaults
{
    public const string Scheme = "ApiKey";
    public const string HeaderName = "X-Api-Key";
}

public static class ApiKeyAuthExtensions
{
    public static AuthenticationBuilder AddApiKey(this AuthenticationBuilder builder) =>
        builder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationDefaults.Scheme, _ => { });
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IApiKeyTokenService _tokenService;
    private readonly IApiKeyRepository _repository;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyTokenService tokenService,
        IApiKeyRepository repository)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
        _repository = repository;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationDefaults.HeaderName, out var values))
            return AuthenticateResult.NoResult();

        var presented = values.ToString();
        if (string.IsNullOrWhiteSpace(presented))
            return AuthenticateResult.NoResult();

        var hash = _tokenService.ComputeHash(presented);
        var apiKey = await _repository.GetByHashAsync(hash, Context.RequestAborted);

        var now = DateTime.UtcNow;
        if (apiKey is null || !apiKey.IsActive(now))
            return AuthenticateResult.Fail("Invalid or inactive API key.");

        // LastUsedAt is audit-only; a persistence failure must not deny an otherwise valid request.
        apiKey.MarkUsed(now);
        try
        {
            await _repository.UpdateAsync(apiKey, Context.RequestAborted);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to persist LastUsedAt for API key {ApiKeyId}", apiKey.Id);
        }

        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", apiKey.UserId),
                new Claim("api_key_id", apiKey.Id),
            },
            Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}

using System.Security.Claims;
using System.Text.Encodings.Web;
using FluentAssertions;
using InvestmentApp.Api.Auth;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace InvestmentApp.Api.Tests.Auth;

public class ApiKeyAuthenticationHandlerTests
{
    private readonly Mock<IApiKeyTokenService> _tokenService = new();
    private readonly Mock<IApiKeyRepository> _repository = new();

    private const string PresentedToken = "imk_abcdefghijklmnop";
    private const string ComputedHash = "deadbeefhash";
    private const string UserId = "user-123";

    private async Task<(AuthenticateResult result, DefaultHttpContext context)> AuthenticateAsync(
        Action<DefaultHttpContext> configure)
    {
        var handler = new ApiKeyAuthenticationHandler(
            Mock.Of<IOptionsMonitor<AuthenticationSchemeOptions>>(m =>
                m.Get(It.IsAny<string>()) == new AuthenticationSchemeOptions()),
            new LoggerFactory(),
            UrlEncoder.Default,
            _tokenService.Object,
            _repository.Object);

        var context = new DefaultHttpContext();
        configure(context);

        var scheme = new AuthenticationScheme(
            ApiKeyAuthenticationDefaults.Scheme, null, typeof(ApiKeyAuthenticationHandler));
        await handler.InitializeAsync(scheme, context);
        var result = await handler.AuthenticateAsync();
        return (result, context);
    }

    private static ApiKey ActiveKey() =>
        ApiKey.Create(UserId, "npu", ComputedHash, "imk_abcdefgh", DateTime.UtcNow.AddDays(30));

    [Fact]
    public async Task MissingHeader_ReturnsNoResult()
    {
        var (result, _) = await AuthenticateAsync(_ => { });

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyHeader_ReturnsNoResult()
    {
        var (result, _) = await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = "");

        result.None.Should().BeTrue();
    }

    [Fact]
    public async Task UnknownKey_ReturnsFail()
    {
        _tokenService.Setup(s => s.ComputeHash(PresentedToken)).Returns(ComputedHash);
        _repository.Setup(r => r.GetByHashAsync(ComputedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        var (result, _) = await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = PresentedToken);

        result.Failure!.Message.Should().Contain("Invalid or inactive API key.");
    }

    [Fact]
    public async Task RevokedKey_ReturnsFail()
    {
        var key = ActiveKey();
        key.Revoke();
        _tokenService.Setup(s => s.ComputeHash(PresentedToken)).Returns(ComputedHash);
        _repository.Setup(r => r.GetByHashAsync(ComputedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        var (result, _) = await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = PresentedToken);

        result.Failure!.Message.Should().Contain("Invalid or inactive API key.");
    }

    [Fact]
    public async Task ExpiredKey_ReturnsFail()
    {
        var key = ActiveKey();
        // Create() forbids past expiry; force it past to exercise the expiry branch.
        typeof(ApiKey).GetProperty(nameof(ApiKey.ExpiresAt))!
            .SetValue(key, DateTime.UtcNow.AddDays(-1));
        _tokenService.Setup(s => s.ComputeHash(PresentedToken)).Returns(ComputedHash);
        _repository.Setup(r => r.GetByHashAsync(ComputedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        var (result, _) = await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = PresentedToken);

        result.Failure!.Message.Should().Contain("Invalid or inactive API key.");
    }

    [Fact]
    public async Task ValidKey_ReturnsSuccess_WithSubClaim()
    {
        _tokenService.Setup(s => s.ComputeHash(PresentedToken)).Returns(ComputedHash);
        _repository.Setup(r => r.GetByHashAsync(ComputedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActiveKey());

        var (result, _) = await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = PresentedToken);

        result.Succeeded.Should().BeTrue();
        result.Principal!.FindFirst("sub")!.Value.Should().Be(UserId);
    }

    [Fact]
    public async Task ValidKey_PersistsLastUsed()
    {
        var key = ActiveKey();
        _tokenService.Setup(s => s.ComputeHash(PresentedToken)).Returns(ComputedHash);
        _repository.Setup(r => r.GetByHashAsync(ComputedHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        await AuthenticateAsync(ctx =>
            ctx.Request.Headers[ApiKeyAuthenticationDefaults.HeaderName] = PresentedToken);

        key.LastUsedAt.Should().NotBeNull();
        _repository.Verify(r => r.UpdateAsync(key, It.IsAny<CancellationToken>()), Times.Once);
    }
}

using FluentAssertions;
using InvestmentApp.Api.Middleware;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace InvestmentApp.Api.Tests.Middleware;

public class ImpersonationTokenReaderTests
{
    private static string SignedToken(Claim[] claims, DateTime? expires = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("test-signing-key-at-least-32-bytes!!"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddMinutes(-10),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    [Fact]
    public void ReturnsImpersonationId_WhenTokenCarriesClaim_EvenIfExpired()
    {
        var raw = SignedToken(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "target-1"),
            new Claim("impersonation_id", "audit-xyz"),
            new Claim("amr", "impersonate")
        }, expires: DateTime.UtcNow.AddMinutes(-1));

        var ok = ImpersonationValidationMiddleware
            .TryReadImpersonationIdWithoutValidation(raw, out var id);

        ok.Should().BeTrue();
        id.Should().Be("audit-xyz");
    }

    [Fact]
    public void ReturnsFalse_WhenTokenLacksImpersonationClaim()
    {
        var raw = SignedToken(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, "user-1"),
            new Claim("role", "User")
        });

        var ok = ImpersonationValidationMiddleware
            .TryReadImpersonationIdWithoutValidation(raw, out var id);

        ok.Should().BeFalse();
        id.Should().BeEmpty();
    }

    [Fact]
    public void ReturnsFalse_WhenTokenIsMalformed()
    {
        var ok = ImpersonationValidationMiddleware
            .TryReadImpersonationIdWithoutValidation("not-a-jwt", out var id);

        ok.Should().BeFalse();
        id.Should().BeEmpty();
    }

    [Fact]
    public void ReturnsFalse_WhenTokenIsEmpty()
    {
        var ok = ImpersonationValidationMiddleware
            .TryReadImpersonationIdWithoutValidation("", out var id);

        ok.Should().BeFalse();
        id.Should().BeEmpty();
    }
}

using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class JwtServiceImpersonationTests
{
    private readonly JwtService _sut;

    public JwtServiceImpersonationTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "super-secret-test-key-must-be-at-least-32-bytes-long!!",
                ["Jwt:Issuer"] = "InvestmentApp",
                ["Jwt:Audience"] = "InvestmentAppUsers",
                ["Jwt:ExpiryInMinutes"] = "60"
            })
            .Build();
        _sut = new JwtService(config);
    }

    private static User NewUser(string id, string email, UserRole role = UserRole.User)
    {
        var user = new User(email, $"Name-{id}", null, "google");
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, id);
        if (role == UserRole.Admin) user.PromoteToAdmin();
        return user;
    }

    [Fact]
    public void GenerateToken_IncludesRoleClaim()
    {
        var admin = NewUser("admin-1", "admin@example.com", UserRole.Admin);

        var token = _sut.GenerateToken(admin);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        parsed.Claims.Should().Contain(c => c.Type == "role" && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_RegularUser_RoleClaimIsUser()
    {
        var user = NewUser("user-1", "user@example.com", UserRole.User);

        var token = _sut.GenerateToken(user);

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        parsed.Claims.Should().Contain(c => c.Type == "role" && c.Value == "User");
    }

    [Fact]
    public void CreateImpersonationToken_ContainsAllRequiredClaims()
    {
        var target = NewUser("target-1", "target@example.com");

        var token = _sut.CreateImpersonationToken("admin-1", target, "audit-xyz");

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        parsed.Subject.Should().Be("target-1");
        parsed.Claims.Should().Contain(c => c.Type == "actor" && c.Value == "admin-1");
        parsed.Claims.Should().Contain(c => c.Type == "impersonation_id" && c.Value == "audit-xyz");
        parsed.Claims.Should().Contain(c => c.Type == "amr" && c.Value == "impersonate");
        parsed.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "target@example.com");
    }

    [Fact]
    public void CreateImpersonationToken_TtlIsApproximatelyOneHour()
    {
        var target = NewUser("target-1", "target@example.com");

        var token = _sut.CreateImpersonationToken("admin-1", target, "audit-xyz");

        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var expectedExpiry = DateTime.UtcNow.AddHours(1);
        parsed.ValidTo.Should().BeCloseTo(expectedExpiry, TimeSpan.FromMinutes(1));
    }
}

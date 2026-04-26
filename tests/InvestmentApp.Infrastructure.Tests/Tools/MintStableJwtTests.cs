using FluentAssertions;
using InvestmentApp.Infrastructure.Repositories;
using InvestmentApp.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using Xunit.Abstractions;

namespace InvestmentApp.Infrastructure.Tests.Tools;

public class MintStableJwtTests
{
    private readonly ITestOutputHelper _output;

    public MintStableJwtTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void EnsureEmailAllowed_Throws_When_Email_Not_In_Hardcoded_Allowlist()
    {
        var act = () => StableJwtMint.EnsureEmailAllowed("evil@hacker.com");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*evil@hacker.com*allowlist*");
    }

    [Fact]
    public void EnsureEmailAllowed_Accepts_Known_Test_Email_Case_Insensitive()
    {
        var act = () => StableJwtMint.EnsureEmailAllowed("InvestMate.Support@Gmail.com");

        act.Should().NotThrow();
    }

    [Fact]
    public async Task Mint_Generates_Token_With_Stable_Sub_When_Conditions_Met()
    {
        var email = Environment.GetEnvironmentVariable("MINT_EMAIL");
        var connStr = Environment.GetEnvironmentVariable("MINT_MONGO_CONN");
        var dbName = Environment.GetEnvironmentVariable("MINT_MONGO_DB");
        var jwtKey = Environment.GetEnvironmentVariable("MINT_JWT_KEY");
        var jwtIssuer = Environment.GetEnvironmentVariable("MINT_JWT_ISSUER");
        var jwtAudience = Environment.GetEnvironmentVariable("MINT_JWT_AUDIENCE");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(connStr)
            || string.IsNullOrWhiteSpace(dbName) || string.IsNullOrWhiteSpace(jwtKey)
            || string.IsNullOrWhiteSpace(jwtIssuer) || string.IsNullOrWhiteSpace(jwtAudience))
        {
            _output.WriteLine("Skipped — set MINT_EMAIL, MINT_MONGO_CONN, MINT_MONGO_DB, MINT_JWT_KEY, MINT_JWT_ISSUER, MINT_JWT_AUDIENCE to mint a stable JWT.");
            return;
        }

        var jwt1 = await StableJwtMint.MintAsync(email, connStr, dbName, jwtKey, jwtIssuer, jwtAudience);
        var jwt2 = await StableJwtMint.MintAsync(email, connStr, dbName, jwtKey, jwtIssuer, jwtAudience);

        jwt1.Should().NotBeNullOrWhiteSpace();
        jwt2.Should().NotBeNullOrWhiteSpace();

        var parsed1 = new JwtSecurityTokenHandler().ReadJwtToken(jwt1);
        var parsed2 = new JwtSecurityTokenHandler().ReadJwtToken(jwt2);

        parsed1.Subject.Should().NotBeNullOrWhiteSpace();
        parsed1.Subject.Should().Be(parsed2.Subject, "stable user_id is the whole point — both mints must yield the same sub claim");
        parsed1.ValidTo.Should().BeAfter(DateTime.UtcNow.AddDays(29), "long-lived (~30 days) so AI doesn't re-mint every session");

        _output.WriteLine($"=== JWT (stable sub={parsed1.Subject}, valid until {parsed1.ValidTo:u}) ===");
        _output.WriteLine(jwt1);
        _output.WriteLine("=== END ===");
    }
}

internal static class StableJwtMint
{
    private static readonly string[] ALLOWED_EMAILS =
    {
        "investmate.support@gmail.com"
    };

    public static void EnsureEmailAllowed(string email)
    {
        if (!ALLOWED_EMAILS.Contains(email, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to mint token for '{email}'. Only test accounts in the hardcoded allowlist are permitted. " +
                "To add a new test email, edit StableJwtMint.ALLOWED_EMAILS in source and submit a PR.");
        }
    }

    public static async Task<string> MintAsync(string email, string mongoConn, string dbName,
        string jwtKey, string jwtIssuer, string jwtAudience)
    {
        EnsureEmailAllowed(email);

        var database = new MongoClient(mongoConn).GetDatabase(dbName);
        var userRepo = new UserRepository(database);

        var user = await userRepo.GetByEmailAsync(email)
            ?? throw new InvalidOperationException(
                $"User '{email}' not found in database '{dbName}'. Login once via Google first to seed the user record.");

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = jwtKey,
                ["Jwt:Issuer"] = jwtIssuer,
                ["Jwt:Audience"] = jwtAudience,
                ["Jwt:ExpiryInMinutes"] = (60 * 24 * 30).ToString()
            })
            .Build();

        return new JwtService(config).GenerateToken(user);
    }
}

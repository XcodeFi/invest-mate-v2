using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class ApiKeyTests
{
    private static ApiKey CreateValid(DateTime? expiresAt = null) =>
        ApiKey.Create("user-1", "NPU daily digest", "hash-abc", "imk_ab12cd34",
            expiresAt ?? DateTime.UtcNow.AddDays(90));

    [Fact]
    public void Create_ValidParameters_SetsFields()
    {
        var expires = DateTime.UtcNow.AddDays(90);

        var key = ApiKey.Create("user-1", "NPU daily digest", "hash-abc", "imk_ab12cd34", expires);

        key.Id.Should().NotBeNullOrEmpty();
        key.UserId.Should().Be("user-1");
        key.Name.Should().Be("NPU daily digest");
        key.KeyHash.Should().Be("hash-abc");
        key.Prefix.Should().Be("imk_ab12cd34");
        key.ExpiresAt.Should().Be(expires);
        key.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        key.LastUsedAt.Should().BeNull();
        key.RevokedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_InvalidUserId_Throws(string? userId)
    {
        var act = () => ApiKey.Create(userId!, "name", "hash", "imk_x", DateTime.UtcNow.AddDays(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ExpiryInPast_Throws()
    {
        var act = () => ApiKey.Create("user-1", "name", "hash", "imk_x", DateTime.UtcNow.AddMinutes(-1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void IsActive_NotRevokedNotExpired_ReturnsTrue()
    {
        var key = CreateValid(DateTime.UtcNow.AddDays(10));

        key.IsActive(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsActive_PastExpiry_ReturnsFalse()
    {
        var key = CreateValid(DateTime.UtcNow.AddDays(1));

        key.IsActive(DateTime.UtcNow.AddDays(2)).Should().BeFalse();
    }

    [Fact]
    public void IsActive_Revoked_ReturnsFalse()
    {
        var key = CreateValid();

        key.Revoke();

        key.IsActive(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void Revoke_SetsRevokedAtAndBumpsVersion()
    {
        var key = CreateValid();
        var before = key.Version;

        key.Revoke();

        key.RevokedAt.Should().NotBeNull();
        key.Version.Should().Be(before + 1);
    }

    [Fact]
    public void Revoke_Twice_IsIdempotent()
    {
        var key = CreateValid();
        key.Revoke();
        var firstRevokedAt = key.RevokedAt;

        key.Revoke();

        key.RevokedAt.Should().Be(firstRevokedAt);
    }

    [Fact]
    public void MarkUsed_SetsLastUsedAt()
    {
        var key = CreateValid();
        var when = DateTime.UtcNow;

        key.MarkUsed(when);

        key.LastUsedAt.Should().Be(when);
    }
}

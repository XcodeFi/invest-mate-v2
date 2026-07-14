using FluentAssertions;
using InvestmentApp.Application.ApiKeys.Queries.GetApiKeys;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.ApiKeys;

public class GetApiKeysQueryHandlerTests
{
    private readonly Mock<IApiKeyRepository> _repo = new();
    private readonly GetApiKeysQueryHandler _handler;

    public GetApiKeysQueryHandlerTests()
    {
        _handler = new GetApiKeysQueryHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_ReturnsUserKeys_WithMetadataButNoHash()
    {
        var active = ApiKey.Create("user-1", "NPU", "hash-active", "imk_aaaa", DateTime.UtcNow.AddDays(30));
        var revoked = ApiKey.Create("user-1", "Old", "hash-revoked", "imk_bbbb", DateTime.UtcNow.AddDays(30));
        revoked.Revoke();

        _repo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { active, revoked });

        var result = (await _handler.Handle(new GetApiKeysQuery { UserId = "user-1" }, CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
        var first = result.Single(k => k.Name == "NPU");
        first.Prefix.Should().Be("imk_aaaa");
        first.IsActive.Should().BeTrue();
        result.Single(k => k.Name == "Old").IsActive.Should().BeFalse();

        // Compile-time guarantee that the DTO never carries the hash.
        typeof(ApiKeyDto).GetProperty("KeyHash").Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsEmpty_WhenUserHasNoKeys()
    {
        _repo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApiKey>());

        var result = await _handler.Handle(new GetApiKeysQuery { UserId = "user-1" }, CancellationToken.None);

        result.Should().BeEmpty();
    }
}

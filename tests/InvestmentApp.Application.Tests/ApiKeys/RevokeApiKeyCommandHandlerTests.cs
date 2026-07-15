using FluentAssertions;
using InvestmentApp.Application.ApiKeys.Commands.RevokeApiKey;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.ApiKeys;

public class RevokeApiKeyCommandHandlerTests
{
    private readonly Mock<IApiKeyRepository> _repo = new();
    private readonly RevokeApiKeyCommandHandler _handler;

    public RevokeApiKeyCommandHandlerTests()
    {
        _handler = new RevokeApiKeyCommandHandler(_repo.Object);
    }

    [Fact]
    public async Task Handle_RevokesOwnedKey_AndPersists()
    {
        var key = ApiKey.Create("user-1", "NPU", "hash", "imk_aaaa", DateTime.UtcNow.AddDays(30));
        _repo.Setup(r => r.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);

        await _handler.Handle(new RevokeApiKeyCommand { Id = key.Id, UserId = "user-1" }, CancellationToken.None);

        key.RevokedAt.Should().NotBeNull();
        _repo.Verify(r => r.UpdateAsync(It.Is<ApiKey>(k => k.Id == key.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Throws_WhenKeyNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((ApiKey?)null);

        var act = () => _handler.Handle(new RevokeApiKeyCommand { Id = "missing", UserId = "user-1" }, CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Throws_WhenNotOwner()
    {
        var key = ApiKey.Create("owner", "NPU", "hash", "imk_aaaa", DateTime.UtcNow.AddDays(30));
        _repo.Setup(r => r.GetByIdAsync(key.Id, It.IsAny<CancellationToken>())).ReturnsAsync(key);

        var act = () => _handler.Handle(new RevokeApiKeyCommand { Id = key.Id, UserId = "attacker" }, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _repo.Verify(r => r.UpdateAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

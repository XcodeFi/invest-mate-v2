using FluentAssertions;
using InvestmentApp.Application.ApiKeys.Commands.CreateApiKey;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.ApiKeys;

public class CreateApiKeyCommandHandlerTests
{
    private readonly Mock<IApiKeyRepository> _repo = new();
    private readonly Mock<IApiKeyTokenService> _tokenService = new();
    private readonly CreateApiKeyCommandHandler _handler;

    private static readonly GeneratedApiKey Generated =
        new("imk_plaintext-token", "hash-xyz", "imk_plainte");

    public CreateApiKeyCommandHandlerTests()
    {
        _tokenService.Setup(s => s.Generate()).Returns(Generated);
        _handler = new CreateApiKeyCommandHandler(_repo.Object, _tokenService.Object);
    }

    [Fact]
    public async Task Handle_ReturnsPlaintextTokenOnceWithMetadata()
    {
        var command = new CreateApiKeyCommand { UserId = "user-1", Name = "NPU", ExpiresInDays = 90 };

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Token.Should().Be(Generated.Plaintext);
        result.Prefix.Should().Be(Generated.Prefix);
        result.Name.Should().Be("NPU");
        result.Id.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(90), TimeSpan.FromSeconds(5));
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_PersistsEntityWithHashNotPlaintext()
    {
        var command = new CreateApiKeyCommand { UserId = "user-1", Name = "NPU", ExpiresInDays = 30 };

        await _handler.Handle(command, CancellationToken.None);

        _repo.Verify(r => r.AddAsync(
            It.Is<ApiKey>(k =>
                k.UserId == "user-1" &&
                k.Name == "NPU" &&
                k.KeyHash == Generated.Hash &&
                k.Prefix == Generated.Prefix),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}

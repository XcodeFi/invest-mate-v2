using FluentAssertions;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class ApiKeyTokenServiceTests
{
    private readonly ApiKeyTokenService _service = new();

    [Fact]
    public void Generate_PlaintextStartsWithPrefix()
    {
        var generated = _service.Generate();

        generated.Plaintext.Should().StartWith("imk_");
    }

    [Fact]
    public void Generate_HashMatchesComputeHashOfPlaintext()
    {
        var generated = _service.Generate();

        generated.Hash.Should().Be(_service.ComputeHash(generated.Plaintext));
    }

    [Fact]
    public void Generate_DisplayPrefixIsHeadOfPlaintext()
    {
        var generated = _service.Generate();

        generated.Prefix.Should().HaveLength(12);
        generated.Plaintext.Should().StartWith(generated.Prefix);
    }

    [Fact]
    public void Generate_TwoCalls_ProduceDifferentTokens()
    {
        var a = _service.Generate();
        var b = _service.Generate();

        a.Plaintext.Should().NotBe(b.Plaintext);
        a.Hash.Should().NotBe(b.Hash);
    }

    [Fact]
    public void ComputeHash_IsDeterministicAnd64HexChars()
    {
        const string token = "imk_sample-token";

        var hash1 = _service.ComputeHash(token);
        var hash2 = _service.ComputeHash(token);

        hash1.Should().Be(hash2);
        hash1.Should().HaveLength(64);
        hash1.Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Fact]
    public void ComputeHash_DifferentInput_DifferentHash()
    {
        _service.ComputeHash("imk_a").Should().NotBe(_service.ComputeHash("imk_b"));
    }
}

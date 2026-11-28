using FluentAssertions;
using InvestmentApp.Domain.Constants;

namespace InvestmentApp.Domain.Tests.Constants;

public class Vn30BasketTests
{
    [Fact]
    public void has_30_distinct_uppercase_symbols()
    {
        Vn30Basket.Symbols.Should().HaveCount(30);
        Vn30Basket.Symbols.Should().OnlyHaveUniqueItems();
        Vn30Basket.Symbols.Should().OnlyContain(s => s == s.ToUpperInvariant() && s.Length >= 3 && s.Length <= 4);
    }

    [Theory]
    [InlineData("FPT", true)]
    [InlineData("fpt", true)]
    [InlineData(" vnm ", true)]
    [InlineData("HHV", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsMember_ignores_case_and_surrounding_spaces(string? symbol, bool expected)
    {
        Vn30Basket.IsMember(symbol).Should().Be(expected);
    }
}

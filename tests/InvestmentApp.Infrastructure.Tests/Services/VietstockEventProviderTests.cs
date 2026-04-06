using FluentAssertions;
using InvestmentApp.Infrastructure.Services.Vietstock;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class VietstockEventProviderTests
{
    [Theory]
    [InlineData("/Date(1774595211000)/", 2026, 3, 27)]
    [InlineData("/Date(1750698000000)/", 2025, 6, 23)]
    [InlineData("/Date(0)/", 1970, 1, 1)]
    public void ParseDotNetDate_ValidFormat_ReturnsCorrectDateTime(string input, int year, int month, int day)
    {
        var result = VietstockEventProvider.ParseDotNetDate(input);

        result.Year.Should().Be(year);
        result.Month.Should().Be(month);
        result.Day.Should().Be(day);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("Date(123)")]
    public void ParseDotNetDate_InvalidFormat_ReturnsMinValue(string? input)
    {
        var result = VietstockEventProvider.ParseDotNetDate(input);

        result.Should().Be(DateTime.MinValue);
    }
}

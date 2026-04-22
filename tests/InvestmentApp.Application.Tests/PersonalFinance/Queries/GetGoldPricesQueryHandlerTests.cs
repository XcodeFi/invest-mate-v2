using FluentAssertions;
using InvestmentApp.Application.Common.Interfaces;
using InvestmentApp.Application.PersonalFinance.Dtos;
using InvestmentApp.Application.PersonalFinance.Queries.GetGoldPrices;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.PersonalFinance.Queries;

public class GetGoldPricesQueryHandlerTests
{
    private readonly Mock<IGoldPriceProvider> _provider = new();
    private readonly GetGoldPricesQueryHandler _handler;

    public GetGoldPricesQueryHandlerTests()
    {
        _handler = new GetGoldPricesQueryHandler(_provider.Object);
    }

    [Fact]
    public async Task Handle_DelegatesToProvider()
    {
        var mockPrices = new List<GoldPriceDto>
        {
            new() { Brand = GoldBrand.SJC, Type = GoldType.Mieng, BuyPrice = 167_000_000m, SellPrice = 169_500_000m, UpdatedAt = DateTime.UtcNow },
            new() { Brand = GoldBrand.PNJ, Type = GoldType.Nhan, BuyPrice = 166_500_000m, SellPrice = 169_500_000m, UpdatedAt = DateTime.UtcNow },
        };
        _provider.Setup(p => p.GetPricesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(mockPrices);

        var result = await _handler.Handle(new GetGoldPricesQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Should().BeEquivalentTo(mockPrices);
        _provider.Verify(p => p.GetPricesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

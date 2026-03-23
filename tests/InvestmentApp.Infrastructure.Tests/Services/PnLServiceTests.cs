using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Domain.ValueObjects;
using InvestmentApp.Infrastructure.Services;
using Moq;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class PnLServiceTests
{
    private readonly Mock<ITradeRepository> _tradeRepoMock;
    private readonly Mock<IPortfolioRepository> _portfolioRepoMock;
    private readonly Mock<IStockPriceService> _priceServiceMock;
    private readonly PnLService _sut;

    private const string PortfolioId = "portfolio-1";

    public PnLServiceTests()
    {
        _tradeRepoMock = new Mock<ITradeRepository>();
        _portfolioRepoMock = new Mock<IPortfolioRepository>();
        _priceServiceMock = new Mock<IStockPriceService>();
        _sut = new PnLService(_tradeRepoMock.Object, _portfolioRepoMock.Object, _priceServiceMock.Object);
    }

    // ─── Helper methods ─────────────────────────────────────────────────

    private static Portfolio CreatePortfolio(string id = PortfolioId)
    {
        return new Portfolio("user-1", "Test Portfolio", 100_000_000m);
    }

    private static Trade CreateTrade(
        string portfolioId,
        string symbol,
        TradeType tradeType,
        decimal quantity,
        decimal price,
        DateTime? createdAt = null)
    {
        return new Trade(portfolioId, symbol, tradeType, quantity, price, tradeDate: createdAt ?? DateTime.UtcNow);
    }

    private void SetupPortfolioExists(string portfolioId = PortfolioId)
    {
        _portfolioRepoMock
            .Setup(r => r.GetByIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePortfolio(portfolioId));
    }

    private void SetupTrades(IEnumerable<Trade> allTrades, string portfolioId = PortfolioId)
    {
        var tradeList = allTrades.ToList();

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tradeList);

        // Setup per-symbol queries
        var bySymbol = tradeList.GroupBy(t => t.Symbol);
        foreach (var group in bySymbol)
        {
            _tradeRepoMock
                .Setup(r => r.GetByPortfolioIdAndSymbolAsync(portfolioId, group.Key, It.IsAny<CancellationToken>()))
                .ReturnsAsync(group.ToList());
        }
    }

    private void SetupPrice(string symbol, decimal price, string currency = "USD")
    {
        _priceServiceMock
            .Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == symbol.ToUpper())))
            .ReturnsAsync(new Money(price, currency));
    }

    // ─── Tests: CalculatePortfolioPnLAsync ───────────────────────────────

    [Fact]
    public async Task CalculatePortfolioPnL_PortfolioNotFound_ThrowsArgumentException()
    {
        // Arrange
        _portfolioRepoMock
            .Setup(r => r.GetByIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Portfolio?)null);

        // Act
        var act = () => _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName("portfolioId");
    }

    [Fact]
    public async Task CalculatePortfolioPnL_EmptyPortfolio_ReturnsZeroSummary()
    {
        // Arrange
        SetupPortfolioExists();
        SetupTrades(Enumerable.Empty<Trade>());

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        result.TotalRealizedPnL.Should().Be(0);
        result.TotalUnrealizedPnL.Should().Be(0);
        result.TotalPortfolioValue.Should().Be(0);
        result.TotalInvested.Should().Be(0);
        result.Positions.Should().BeEmpty();
    }

    [Fact]
    public async Task CalculatePortfolioPnL_SingleBuyTrade_CalculatesUnrealizedPnL()
    {
        // Arrange: BUY 100 VNM @ 80,000; current price = 85,000
        SetupPortfolioExists();
        var buyTrade = CreateTrade(PortfolioId, "VNM", TradeType.BUY, 100, 80_000m);
        SetupTrades(new[] { buyTrade });
        SetupPrice("VNM", 85_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        result.Positions.Should().HaveCount(1);
        var position = result.Positions[0];
        position.Symbol.Should().Be("VNM");
        position.Quantity.Should().Be(100);
        position.AverageCost.Should().Be(80_000m);
        position.CurrentPrice.Should().Be(85_000m);

        // Unrealized PnL = (85,000 - 80,000) * 100 = 500,000
        position.UnrealizedPnL.Should().Be(500_000m);
        position.RealizedPnL.Should().Be(0);

        // Portfolio totals
        result.TotalPortfolioValue.Should().Be(100 * 85_000m); // MarketValue
        result.TotalInvested.Should().Be(100 * 80_000m);       // TotalCost
    }

    [Fact]
    public async Task CalculatePortfolioPnL_BuyThenSell_CalculatesRealizedPnL()
    {
        // Arrange: BUY 200 HPG @ 25,000; SELL 100 HPG @ 30,000
        // avgCost = 25,000
        // realizedPnL = 100 * (30,000 - 25,000) = 500,000
        // remaining qty = 100, current price = 28,000
        // unrealizedPnL = (28,000 - 25,000) * 100 = 300,000
        SetupPortfolioExists();

        var buy = CreateTrade(PortfolioId, "HPG", TradeType.BUY, 200, 25_000m, DateTime.UtcNow.AddDays(-10));
        var sell = CreateTrade(PortfolioId, "HPG", TradeType.SELL, 100, 30_000m, DateTime.UtcNow.AddDays(-5));
        SetupTrades(new[] { buy, sell });
        SetupPrice("HPG", 28_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        result.Positions.Should().HaveCount(1);
        var position = result.Positions[0];
        position.Quantity.Should().Be(100);          // 200 - 100
        position.AverageCost.Should().Be(25_000m);   // Only BUY trades averaged
        position.RealizedPnL.Should().Be(500_000m);  // 100 * (30k - 25k)
        position.UnrealizedPnL.Should().Be(300_000m); // (28k - 25k) * 100

        result.TotalRealizedPnL.Should().Be(500_000m);
        result.TotalUnrealizedPnL.Should().Be(300_000m);
    }

    [Fact]
    public async Task CalculatePortfolioPnL_MultipleBuysDifferentPrices_CalculatesWeightedAvgCost()
    {
        // Arrange: BUY 100 FPT @ 90,000; BUY 200 FPT @ 95,000
        // totalCost = 100*90,000 + 200*95,000 = 9,000,000 + 19,000,000 = 28,000,000
        // totalQty = 300
        // avgCost = 28,000,000 / 300 = 93,333.333...
        SetupPortfolioExists();

        var buy1 = CreateTrade(PortfolioId, "FPT", TradeType.BUY, 100, 90_000m, DateTime.UtcNow.AddDays(-20));
        var buy2 = CreateTrade(PortfolioId, "FPT", TradeType.BUY, 200, 95_000m, DateTime.UtcNow.AddDays(-10));
        SetupTrades(new[] { buy1, buy2 });
        SetupPrice("FPT", 100_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        var position = result.Positions.Single();
        position.Quantity.Should().Be(300);

        // avgCost = 28,000,000 / 300 ≈ 93,333.333...
        var expectedAvgCost = 28_000_000m / 300m;
        position.AverageCost.Should().BeApproximately(expectedAvgCost, 0.01m);

        // Unrealized = (100,000 - 93,333.33) * 300 = 2,000,000 (approx)
        position.UnrealizedPnL.Should().BeApproximately(2_000_000m, 0.1m);
    }

    [Fact]
    public async Task CalculatePortfolioPnL_MultipleSymbols_AggregatesAllPositions()
    {
        // Arrange: VNM (BUY 100 @ 80k, current 85k) + HPG (BUY 200 @ 25k, current 28k)
        SetupPortfolioExists();

        var vnmBuy = CreateTrade(PortfolioId, "VNM", TradeType.BUY, 100, 80_000m);
        var hpgBuy = CreateTrade(PortfolioId, "HPG", TradeType.BUY, 200, 25_000m);

        var allTrades = new[] { vnmBuy, hpgBuy };

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allTrades);

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { vnmBuy });

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hpgBuy });

        SetupPrice("VNM", 85_000m);
        SetupPrice("HPG", 28_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        result.Positions.Should().HaveCount(2);

        // VNM: marketValue = 100 * 85,000 = 8,500,000; cost = 100 * 80,000 = 8,000,000
        // HPG: marketValue = 200 * 28,000 = 5,600,000; cost = 200 * 25,000 = 5,000,000
        var expectedTotalValue = (100 * 85_000m) + (200 * 28_000m);   // 14,100,000
        var expectedTotalCost = (100 * 80_000m) + (200 * 25_000m);    // 13,000,000

        result.TotalPortfolioValue.Should().Be(expectedTotalValue);
        result.TotalInvested.Should().Be(expectedTotalCost);
        result.TotalUnrealizedPnL.Should().Be(expectedTotalValue - expectedTotalCost); // 1,100,000
    }

    [Fact]
    public async Task CalculatePortfolioPnL_PriceServiceThrowsForOneSymbol_SkipsAndContinues()
    {
        // Arrange: VNM has valid price, HPG price fails
        SetupPortfolioExists();

        var vnmBuy = CreateTrade(PortfolioId, "VNM", TradeType.BUY, 100, 80_000m);
        var hpgBuy = CreateTrade(PortfolioId, "HPG", TradeType.BUY, 200, 25_000m);

        var allTrades = new[] { vnmBuy, hpgBuy };

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAsync(PortfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(allTrades);

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { vnmBuy });

        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "HPG", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { hpgBuy });

        SetupPrice("VNM", 85_000m);
        // HPG price throws
        _priceServiceMock
            .Setup(s => s.GetCurrentPriceAsync(It.Is<StockSymbol>(ss => ss.Value == "HPG")))
            .ThrowsAsync(new Exception("Price service unavailable for HPG"));

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert — only VNM should be present, HPG silently skipped
        result.Positions.Should().HaveCount(1);
        result.Positions[0].Symbol.Should().Be("VNM");
        result.TotalPortfolioValue.Should().Be(100 * 85_000m);
    }

    [Fact]
    public async Task CalculatePortfolioPnL_FullSellOut_RemainingQuantityIsZero()
    {
        // Arrange: BUY 100 MWG @ 50,000; SELL 100 MWG @ 60,000; current price = 65,000
        // avgCost = 50,000; realizedPnL = 100 * (60k - 50k) = 1,000,000; remaining qty = 0
        SetupPortfolioExists();

        var buy = CreateTrade(PortfolioId, "MWG", TradeType.BUY, 100, 50_000m, DateTime.UtcNow.AddDays(-10));
        var sell = CreateTrade(PortfolioId, "MWG", TradeType.SELL, 100, 60_000m, DateTime.UtcNow.AddDays(-5));
        SetupTrades(new[] { buy, sell });
        SetupPrice("MWG", 65_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        var position = result.Positions.Single();
        position.Quantity.Should().Be(0);
        position.RealizedPnL.Should().Be(1_000_000m);
        // MarketValue = 0 * 65,000 = 0
        position.MarketValue.Should().Be(0);
        // UnrealizedPnL = (65,000 - 50,000) * 0 = 0
        position.UnrealizedPnL.Should().Be(0);
    }

    [Fact]
    public async Task CalculatePortfolioPnL_SellAtLoss_NegativeRealizedPnL()
    {
        // Arrange: BUY 100 TCB @ 30,000; SELL 50 TCB @ 25,000; current price = 27,000
        // avgCost = 30,000; realizedPnL = 50 * (25,000 - 30,000) = -250,000
        // remaining qty = 50
        SetupPortfolioExists();

        var buy = CreateTrade(PortfolioId, "TCB", TradeType.BUY, 100, 30_000m, DateTime.UtcNow.AddDays(-10));
        var sell = CreateTrade(PortfolioId, "TCB", TradeType.SELL, 50, 25_000m, DateTime.UtcNow.AddDays(-5));
        SetupTrades(new[] { buy, sell });
        SetupPrice("TCB", 27_000m);

        // Act
        var result = await _sut.CalculatePortfolioPnLAsync(PortfolioId);

        // Assert
        var position = result.Positions.Single();
        position.Quantity.Should().Be(50);
        position.AverageCost.Should().Be(30_000m);
        position.RealizedPnL.Should().Be(-250_000m);  // Loss
        // Unrealized = (27,000 - 30,000) * 50 = -150,000
        position.UnrealizedPnL.Should().Be(-150_000m);
        result.TotalRealizedPnL.Should().BeNegative();
    }

    // ─── Tests: CalculatePositionPnLAsync ────────────────────────────────

    [Fact]
    public async Task CalculatePositionPnL_NoTradesForSymbol_ThrowsArgumentException()
    {
        // Arrange
        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "XYZ", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Trade>());

        // Act
        var act = () => _sut.CalculatePositionPnLAsync(PortfolioId, new StockSymbol("XYZ"));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CalculatePositionPnL_SingleBuy_ReturnsCorrectPosition()
    {
        // Arrange: BUY 500 ACB @ 22,000; current price = 24,000
        var buy = CreateTrade(PortfolioId, "ACB", TradeType.BUY, 500, 22_000m);
        _tradeRepoMock
            .Setup(r => r.GetByPortfolioIdAndSymbolAsync(PortfolioId, "ACB", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { buy });
        SetupPrice("ACB", 24_000m);

        // Act
        var result = await _sut.CalculatePositionPnLAsync(PortfolioId, new StockSymbol("ACB"));

        // Assert
        result.Symbol.Should().Be("ACB");
        result.Quantity.Should().Be(500);
        result.AverageCost.Should().Be(22_000m);
        result.CurrentPrice.Should().Be(24_000m);
        result.RealizedPnL.Should().Be(0);
        result.UnrealizedPnL.Should().Be(500 * (24_000m - 22_000m)); // 1,000,000
    }
}

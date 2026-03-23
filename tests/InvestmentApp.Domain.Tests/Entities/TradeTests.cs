using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class TradeTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidBuyTrade_ShouldCreateTrade()
    {
        // Arrange
        var portfolioId = "portfolio-1";
        var symbol = "VNM";
        var quantity = 100m;
        var price = 85000m;
        var fee = 150m;
        var tax = 50m;
        var tradeDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var trade = new Trade(portfolioId, symbol, TradeType.BUY, quantity, price, fee, tax, tradeDate);

        // Assert
        trade.Id.Should().NotBeNullOrEmpty();
        trade.PortfolioId.Should().Be(portfolioId);
        trade.Symbol.Should().Be("VNM");
        trade.TradeType.Should().Be(TradeType.BUY);
        trade.Quantity.Should().Be(quantity);
        trade.Price.Should().Be(price);
        trade.Fee.Should().Be(fee);
        trade.Tax.Should().Be(tax);
        trade.TradeDate.Should().Be(tradeDate);
        trade.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        trade.StrategyId.Should().BeNull();
        trade.TradePlanId.Should().BeNull();
    }

    [Fact]
    public void Constructor_ValidSellTrade_ShouldCreateTrade()
    {
        // Act
        var trade = new Trade("portfolio-1", "FPT", TradeType.SELL, 50, 120000m);

        // Assert
        trade.TradeType.Should().Be(TradeType.SELL);
        trade.Symbol.Should().Be("FPT");
    }

    [Fact]
    public void Constructor_DefaultOptionalParameters_ShouldUseDefaults()
    {
        // Act
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);

        // Assert
        trade.Fee.Should().Be(0m);
        trade.Tax.Should().Be(0m);
        trade.TradeDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_CustomTradeDate_ShouldUseProvidedDate()
    {
        // Arrange
        var customDate = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m, tradeDate: customDate);

        // Assert
        trade.TradeDate.Should().Be(customDate);
    }

    #endregion

    #region Constructor — Symbol Normalization

    [Fact]
    public void Constructor_LowercaseSymbol_ShouldConvertToUpperCase()
    {
        // Act
        var trade = new Trade("portfolio-1", "vnm", TradeType.BUY, 100, 85000m);

        // Assert
        trade.Symbol.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_MixedCaseSymbol_ShouldConvertToUpperCase()
    {
        // Act
        var trade = new Trade("portfolio-1", "Fpt", TradeType.BUY, 100, 85000m);

        // Assert
        trade.Symbol.Should().Be("FPT");
    }

    [Fact]
    public void Constructor_SymbolWithWhitespace_ShouldTrimAndUpperCase()
    {
        // Act
        var trade = new Trade("portfolio-1", "  vnm  ", TradeType.BUY, 100, 85000m);

        // Assert
        trade.Symbol.Should().Be("VNM");
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullPortfolioId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Trade(null!, "VNM", TradeType.BUY, 100, 85000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Trade("portfolio-1", null!, TradeType.BUY, 100, 85000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_ZeroQuantity_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, 0, 85000m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void Constructor_NegativeQuantity_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, -10, 85000m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("quantity");
    }

    [Fact]
    public void Constructor_ZeroPrice_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 0m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("price");
    }

    [Fact]
    public void Constructor_NegativePrice_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, 100, -1000m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("price");
    }

    [Fact]
    public void Constructor_NegativeFee_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m, fee: -1m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("fee");
    }

    [Fact]
    public void Constructor_NegativeTax_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m, tax: -1m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("tax");
    }

    [Fact]
    public void Constructor_ZeroFee_ShouldBeAllowed()
    {
        // Act
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m, fee: 0m);

        // Assert
        trade.Fee.Should().Be(0m);
    }

    [Fact]
    public void Constructor_ZeroTax_ShouldBeAllowed()
    {
        // Act
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m, tax: 0m);

        // Assert
        trade.Tax.Should().Be(0m);
    }

    #endregion

    #region LinkStrategy / UnlinkStrategy

    [Fact]
    public void LinkStrategy_ShouldSetStrategyId()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);
        var strategyId = "strategy-1";

        // Act
        trade.LinkStrategy(strategyId);

        // Assert
        trade.StrategyId.Should().Be(strategyId);
    }

    [Fact]
    public void UnlinkStrategy_ShouldClearStrategyId()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);
        trade.LinkStrategy("strategy-1");

        // Act
        trade.UnlinkStrategy();

        // Assert
        trade.StrategyId.Should().BeNull();
    }

    [Fact]
    public void LinkStrategy_CalledTwice_ShouldOverwriteWithLatest()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);

        // Act
        trade.LinkStrategy("strategy-1");
        trade.LinkStrategy("strategy-2");

        // Assert
        trade.StrategyId.Should().Be("strategy-2");
    }

    #endregion

    #region LinkTradePlan / UnlinkTradePlan

    [Fact]
    public void LinkTradePlan_ShouldSetTradePlanId()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);
        var tradePlanId = "plan-1";

        // Act
        trade.LinkTradePlan(tradePlanId);

        // Assert
        trade.TradePlanId.Should().Be(tradePlanId);
    }

    [Fact]
    public void UnlinkTradePlan_ShouldClearTradePlanId()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);
        trade.LinkTradePlan("plan-1");

        // Act
        trade.UnlinkTradePlan();

        // Assert
        trade.TradePlanId.Should().BeNull();
    }

    [Fact]
    public void LinkTradePlan_CalledTwice_ShouldOverwriteWithLatest()
    {
        // Arrange
        var trade = new Trade("portfolio-1", "VNM", TradeType.BUY, 100, 85000m);

        // Act
        trade.LinkTradePlan("plan-1");
        trade.LinkTradePlan("plan-2");

        // Assert
        trade.TradePlanId.Should().Be("plan-2");
    }

    #endregion
}

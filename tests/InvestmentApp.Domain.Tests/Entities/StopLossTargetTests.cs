using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class StopLossTargetTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateStopLossTarget()
    {
        // Arrange
        var tradeId = "trade-1";
        var portfolioId = "portfolio-1";
        var userId = "user-1";
        var symbol = "VNM";
        var entryPrice = 85_000m;
        var stopLossPrice = 80_000m;
        var targetPrice = 95_000m;

        // Act
        var slt = new StopLossTarget(tradeId, portfolioId, userId, symbol,
            entryPrice, stopLossPrice, targetPrice);

        // Assert
        slt.Id.Should().NotBeNullOrEmpty();
        slt.TradeId.Should().Be(tradeId);
        slt.PortfolioId.Should().Be(portfolioId);
        slt.UserId.Should().Be(userId);
        slt.Symbol.Should().Be(symbol);
        slt.EntryPrice.Should().Be(entryPrice);
        slt.StopLossPrice.Should().Be(stopLossPrice);
        slt.TargetPrice.Should().Be(targetPrice);
        slt.TrailingStopPercent.Should().BeNull();
        slt.TrailingStopPrice.Should().BeNull();
        slt.IsStopLossTriggered.Should().BeFalse();
        slt.IsTargetTriggered.Should().BeFalse();
        slt.TriggeredAt.Should().BeNull();
        slt.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_WithTrailingStopPercent_ShouldSetTrailingStopPercent()
    {
        // Act
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            85_000m, 80_000m, 95_000m, trailingStopPercent: 5m);

        // Assert
        slt.TrailingStopPercent.Should().Be(5m);
    }

    [Fact]
    public void Constructor_NullTradeId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new StopLossTarget(null!, "portfolio-1", "user-1", "VNM",
            85_000m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("tradeId");
    }

    [Fact]
    public void Constructor_NullPortfolioId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new StopLossTarget("trade-1", null!, "user-1", "VNM",
            85_000m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("portfolioId");
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new StopLossTarget("trade-1", "portfolio-1", null!, "VNM",
            85_000m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new StopLossTarget("trade-1", "portfolio-1", "user-1", null!,
            85_000m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_ZeroEntryPrice_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            0m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeEntryPrice_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            -100m, 80_000m, 95_000m);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateStopLoss

    [Fact]
    public void UpdateStopLoss_ShouldUpdateStopLossPriceAndTimestamp()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();
        var newStopLoss = 78_000m;

        // Act
        slt.UpdateStopLoss(newStopLoss);

        // Assert
        slt.StopLossPrice.Should().Be(newStopLoss);
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateStopLoss_ShouldIncrementVersion()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();
        var initialVersion = slt.Version;

        // Act
        slt.UpdateStopLoss(78_000m);

        // Assert
        slt.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region UpdateTarget

    [Fact]
    public void UpdateTarget_ShouldUpdateTargetPriceAndTimestamp()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();
        var newTarget = 100_000m;

        // Act
        slt.UpdateTarget(newTarget);

        // Assert
        slt.TargetPrice.Should().Be(newTarget);
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdateTarget_ShouldIncrementVersion()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();
        var initialVersion = slt.Version;

        // Act
        slt.UpdateTarget(100_000m);

        // Assert
        slt.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region UpdateTrailingStop

    [Fact]
    public void UpdateTrailingStop_ShouldCalculateTrailingStopPrice()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act — 5% trailing on currentPrice 100 → TrailingStopPrice = 100 * (1 - 5/100) = 95
        slt.UpdateTrailingStop(5m, 100m);

        // Assert
        slt.TrailingStopPercent.Should().Be(5m);
        slt.TrailingStopPrice.Should().Be(95m);
    }

    [Fact]
    public void UpdateTrailingStop_TenPercentOnThousand_ShouldReturn900()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act — 10% trailing on 1000 → 1000 * (1 - 10/100) = 900
        slt.UpdateTrailingStop(10m, 1000m);

        // Assert
        slt.TrailingStopPrice.Should().Be(900m);
    }

    [Fact]
    public void UpdateTrailingStop_ShouldIncrementVersion()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();
        var initialVersion = slt.Version;

        // Act
        slt.UpdateTrailingStop(5m, 100m);

        // Assert
        slt.Version.Should().Be(initialVersion + 1);
    }

    [Fact]
    public void UpdateTrailingStop_ShouldUpdateTimestamp()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act
        slt.UpdateTrailingStop(5m, 100m);

        // Assert
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region TriggerStopLoss

    [Fact]
    public void TriggerStopLoss_ShouldSetIsStopLossTriggeredAndTriggeredAt()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act
        slt.TriggerStopLoss();

        // Assert
        slt.IsStopLossTriggered.Should().BeTrue();
        slt.TriggeredAt.Should().NotBeNull();
        slt.TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TriggerStopLoss_ShouldNotAffectTargetTriggered()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act
        slt.TriggerStopLoss();

        // Assert
        slt.IsTargetTriggered.Should().BeFalse();
    }

    #endregion

    #region TriggerTarget

    [Fact]
    public void TriggerTarget_ShouldSetIsTargetTriggeredAndTriggeredAt()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act
        slt.TriggerTarget();

        // Assert
        slt.IsTargetTriggered.Should().BeTrue();
        slt.TriggeredAt.Should().NotBeNull();
        slt.TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        slt.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TriggerTarget_ShouldNotAffectStopLossTriggered()
    {
        // Arrange
        var slt = CreateDefaultStopLossTarget();

        // Act
        slt.TriggerTarget();

        // Assert
        slt.IsStopLossTriggered.Should().BeFalse();
    }

    #endregion

    #region GetRiskRewardRatio

    [Fact]
    public void GetRiskRewardRatio_NormalCase_ShouldReturnCorrectRatio()
    {
        // Arrange — entry=100, stopLoss=90, target=130
        // risk = 100 - 90 = 10, reward = 130 - 100 = 30, ratio = 30/10 = 3
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 90m, 130m);

        // Act
        var ratio = slt.GetRiskRewardRatio();

        // Assert
        ratio.Should().Be(3m);
    }

    [Fact]
    public void GetRiskRewardRatio_RiskIsZero_ShouldReturnZero()
    {
        // Arrange — entry=100, stopLoss=100 → risk = 0
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 100m, 130m);

        // Act
        var ratio = slt.GetRiskRewardRatio();

        // Assert
        ratio.Should().Be(0m);
    }

    [Fact]
    public void GetRiskRewardRatio_StopLossAboveEntry_ShouldReturnZero()
    {
        // Arrange — entry=100, stopLoss=110 → risk = -10, which is <= 0
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 110m, 130m);

        // Act
        var ratio = slt.GetRiskRewardRatio();

        // Assert
        ratio.Should().Be(0m);
    }

    [Fact]
    public void GetRiskRewardRatio_FractionalResult_ShouldReturnCorrectValue()
    {
        // Arrange — entry=100, stopLoss=95, target=110
        // risk = 5, reward = 10, ratio = 2.0
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 95m, 110m);

        // Act
        var ratio = slt.GetRiskRewardRatio();

        // Assert
        ratio.Should().Be(2m);
    }

    #endregion

    #region GetRiskPerShare

    [Fact]
    public void GetRiskPerShare_ShouldReturnEntryMinusStopLoss()
    {
        // Arrange — entry=85000, stopLoss=80000 → risk per share = 5000
        var slt = CreateDefaultStopLossTarget();

        // Act
        var risk = slt.GetRiskPerShare();

        // Assert
        risk.Should().Be(5_000m);
    }

    [Fact]
    public void GetRiskPerShare_StopLossEqualsEntry_ShouldReturnZero()
    {
        // Arrange
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 100m, 130m);

        // Act
        var risk = slt.GetRiskPerShare();

        // Assert
        risk.Should().Be(0m);
    }

    [Fact]
    public void GetRiskPerShare_StopLossAboveEntry_ShouldReturnNegative()
    {
        // Arrange — entry=100, stopLoss=110 → -10
        var slt = new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            100m, 110m, 130m);

        // Act
        var risk = slt.GetRiskPerShare();

        // Assert
        risk.Should().Be(-10m);
    }

    #endregion

    #region Helpers

    private static StopLossTarget CreateDefaultStopLossTarget()
    {
        return new StopLossTarget("trade-1", "portfolio-1", "user-1", "VNM",
            85_000m, 80_000m, 95_000m);
    }

    #endregion
}

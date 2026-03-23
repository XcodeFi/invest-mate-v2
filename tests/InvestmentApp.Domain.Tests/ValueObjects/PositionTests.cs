using FluentAssertions;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Tests.ValueObjects;

public class PositionTests
{
    #region Helpers

    private static StockSymbol Symbol(string value = "VNM") => new StockSymbol(value);
    private static Money VND(decimal amount) => new Money(amount, "VND");

    #endregion

    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreatePositionWithCalculations()
    {
        // Arrange
        var symbol = Symbol("VNM");
        var quantity = 100m;
        var averageCost = VND(85_000m);
        var currentPrice = VND(90_000m);

        // Act
        var position = new Position(symbol, quantity, averageCost, currentPrice);

        // Assert
        position.Symbol.Should().Be(symbol);
        position.Quantity.Should().Be(100m);
        position.AverageCost.Should().Be(averageCost);
        position.CurrentValue.Amount.Should().Be(9_000_000m); // 100 * 90,000
        position.UnrealizedPnL.Amount.Should().Be(500_000m);  // 9,000,000 - (100 * 85,000)
    }

    [Fact]
    public void Constructor_ProfitScenario_ShouldCalculatePositivePnL()
    {
        // Arrange: bought at 80k, now at 100k, qty 200
        var position = new Position(Symbol(), 200m, VND(80_000m), VND(100_000m));

        // Assert
        position.CurrentValue.Amount.Should().Be(20_000_000m);   // 200 * 100,000
        position.UnrealizedPnL.Amount.Should().Be(4_000_000m);   // 20,000,000 - (200 * 80,000)
        position.UnrealizedPnLPercentage.Should().Be(25m);       // (4,000,000 / 16,000,000) * 100
    }

    [Fact]
    public void Constructor_LossScenario_ShouldCalculateNegativePnL()
    {
        // Arrange: bought at 100k, now at 80k, qty 100
        var position = new Position(Symbol(), 100m, VND(100_000m), VND(80_000m));

        // Assert
        position.CurrentValue.Amount.Should().Be(8_000_000m);     // 100 * 80,000
        position.UnrealizedPnL.Amount.Should().Be(-2_000_000m);   // 8,000,000 - (100 * 100,000)
        position.UnrealizedPnLPercentage.Should().Be(-20m);       // (-2,000,000 / 10,000,000) * 100
    }

    [Fact]
    public void Constructor_BreakevenScenario_ShouldCalculateZeroPnL()
    {
        // Arrange: bought at 50k, now at 50k, qty 100
        var position = new Position(Symbol(), 100m, VND(50_000m), VND(50_000m));

        // Assert
        position.CurrentValue.Amount.Should().Be(5_000_000m);
        position.UnrealizedPnL.Amount.Should().Be(0m);
        position.UnrealizedPnLPercentage.Should().Be(0m);
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Position(null!, 100m, VND(85_000m), VND(90_000m));

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_NullAverageCost_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Position(Symbol(), 100m, null!, VND(90_000m));

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("averageCost");
    }

    [Fact]
    public void Constructor_ZeroAverageCost_ShouldSetPnLPercentageToZero()
    {
        // Arrange: edge case where average cost is zero
        var position = new Position(Symbol(), 100m, VND(0m), VND(50_000m));

        // Assert - division by zero guarded: should return 0
        position.UnrealizedPnLPercentage.Should().Be(0m);
    }

    #endregion

    #region UpdateCurrentValue

    [Fact]
    public void UpdateCurrentValue_ShouldRecalculateAllDerivedValues()
    {
        // Arrange: initial price 90k
        var position = new Position(Symbol(), 100m, VND(85_000m), VND(90_000m));

        // Act: price rises to 100k
        position.UpdateCurrentValue(VND(100_000m));

        // Assert
        position.CurrentValue.Amount.Should().Be(10_000_000m);    // 100 * 100,000
        position.UnrealizedPnL.Amount.Should().Be(1_500_000m);    // 10,000,000 - (100 * 85,000)
        position.UnrealizedPnLPercentage.Should().BeApproximately(17.647m, 0.001m); // (1,500,000 / 8,500,000) * 100
    }

    [Fact]
    public void UpdateCurrentValue_PriceDrop_ShouldShowLoss()
    {
        // Arrange: initial price 90k
        var position = new Position(Symbol(), 50m, VND(100_000m), VND(100_000m));

        // Act: price drops to 70k
        position.UpdateCurrentValue(VND(70_000m));

        // Assert
        position.CurrentValue.Amount.Should().Be(3_500_000m);     // 50 * 70,000
        position.UnrealizedPnL.Amount.Should().Be(-1_500_000m);   // 3,500,000 - (50 * 100,000)
        position.UnrealizedPnLPercentage.Should().Be(-30m);       // (-1,500,000 / 5,000,000) * 100
    }

    #endregion

    #region AddShares

    [Fact]
    public void AddShares_ShouldRecalculateAverageCostAndQuantity()
    {
        // Arrange: 100 shares at 80k
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act: add 100 more shares at 100k
        position.AddShares(100m, VND(100_000m));

        // Assert
        // New average = (100*80,000 + 100*100,000) / 200 = 18,000,000 / 200 = 90,000
        position.Quantity.Should().Be(200m);
        position.AverageCost.Amount.Should().Be(90_000m);
    }

    [Fact]
    public void AddShares_SmallAdditionalQuantity_ShouldWeightCorrectly()
    {
        // Arrange: 200 shares at 50k
        var position = new Position(Symbol(), 200m, VND(50_000m), VND(60_000m));

        // Act: add 50 more shares at 70k
        position.AddShares(50m, VND(70_000m));

        // Assert
        // New average = (200*50,000 + 50*70,000) / 250 = (10,000,000 + 3,500,000) / 250 = 54,000
        position.Quantity.Should().Be(250m);
        position.AverageCost.Amount.Should().Be(54_000m);
    }

    [Fact]
    public void AddShares_ZeroQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        var action = () => position.AddShares(0m, VND(100_000m));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("additionalQuantity");
    }

    [Fact]
    public void AddShares_NegativeQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        var action = () => position.AddShares(-10m, VND(100_000m));

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("additionalQuantity");
    }

    #endregion

    #region RemoveShares

    [Fact]
    public void RemoveShares_ValidQuantity_ShouldReduceQuantity()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        position.RemoveShares(30m);

        // Assert
        position.Quantity.Should().Be(70m);
    }

    [Fact]
    public void RemoveShares_AllShares_ShouldSetQuantityToZero()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        position.RemoveShares(100m);

        // Assert
        position.Quantity.Should().Be(0m);
    }

    [Fact]
    public void RemoveShares_MoreThanOwned_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        var action = () => position.RemoveShares(150m);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*more shares than owned*");
    }

    [Fact]
    public void RemoveShares_ZeroQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        var action = () => position.RemoveShares(0m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("quantityToRemove");
    }

    [Fact]
    public void RemoveShares_NegativeQuantity_ShouldThrowArgumentException()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act
        var action = () => position.RemoveShares(-10m);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("quantityToRemove");
    }

    #endregion

    #region IsClosed

    [Fact]
    public void IsClosed_QuantityZero_ShouldReturnTrue()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));
        position.RemoveShares(100m);

        // Act & Assert
        position.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void IsClosed_QuantityGreaterThanZero_ShouldReturnFalse()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act & Assert
        position.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void IsClosed_AfterPartialRemoval_ShouldReturnFalse()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));
        position.RemoveShares(50m);

        // Act & Assert
        position.IsClosed.Should().BeFalse();
    }

    #endregion

    #region Equals and GetHashCode

    [Fact]
    public void Equals_SameSymbol_ShouldBeTrue()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("VNM"), 200m, VND(70_000m), VND(95_000m));

        // Act & Assert
        position1.Equals(position2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentSymbol_ShouldBeFalse()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("FPT"), 100m, VND(80_000m), VND(90_000m));

        // Act & Assert
        position1.Equals(position2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        // Arrange
        var position = new Position(Symbol(), 100m, VND(80_000m), VND(90_000m));

        // Act & Assert
        position.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_SameSymbol_ShouldBeTrue()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        object position2 = new Position(Symbol("VNM"), 50m, VND(60_000m), VND(70_000m));

        // Act & Assert
        position1.Equals(position2).Should().BeTrue();
    }

    [Fact]
    public void GetHashCode_SameSymbol_ShouldBeEqual()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("VNM"), 200m, VND(70_000m), VND(95_000m));

        // Act & Assert
        position1.GetHashCode().Should().Be(position2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentSymbol_ShouldLikelyDiffer()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("FPT"), 100m, VND(80_000m), VND(90_000m));

        // Act & Assert
        position1.GetHashCode().Should().NotBe(position2.GetHashCode());
    }

    #endregion

    #region Operators

    [Fact]
    public void EqualityOperator_SameSymbol_ShouldReturnTrue()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("VNM"), 200m, VND(70_000m), VND(95_000m));

        // Act & Assert
        (position1 == position2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_DifferentSymbol_ShouldReturnTrue()
    {
        // Arrange
        var position1 = new Position(Symbol("VNM"), 100m, VND(80_000m), VND(90_000m));
        var position2 = new Position(Symbol("FPT"), 100m, VND(80_000m), VND(90_000m));

        // Act & Assert
        (position1 != position2).Should().BeTrue();
    }

    #endregion
}

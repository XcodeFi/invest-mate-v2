using FluentAssertions;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Tests.ValueObjects;

public class MoneyTests
{
    #region Constructor

    [Fact]
    public void Constructor_WithAmountOnly_ShouldDefaultCurrencyToVND()
    {
        // Act
        var money = new Money(100_000m);

        // Assert
        money.Amount.Should().Be(100_000m);
        money.Currency.Should().Be("VND");
    }

    [Fact]
    public void Constructor_WithAmountAndCurrency_ShouldSetBoth()
    {
        // Act
        var money = new Money(50.75m, "USD");

        // Assert
        money.Amount.Should().Be(50.75m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_ZeroAmount_ShouldSucceed()
    {
        // Act
        var money = new Money(0m);

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("VND");
    }

    [Fact]
    public void Constructor_NegativeAmount_ShouldSucceed()
    {
        // Act
        var money = new Money(-500m, "VND");

        // Assert
        money.Amount.Should().Be(-500m);
    }

    [Fact]
    public void Constructor_NullCurrency_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new Money(100m, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("currency");
    }

    #endregion

    #region Add

    [Fact]
    public void Add_SameCurrency_ShouldReturnSummedMoney()
    {
        // Arrange
        var money1 = new Money(100_000m, "VND");
        var money2 = new Money(50_000m, "VND");

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(150_000m);
        result.Currency.Should().Be("VND");
    }

    [Fact]
    public void Add_SameCurrencyWithNegative_ShouldReturnCorrectSum()
    {
        // Arrange
        var money1 = new Money(100m, "USD");
        var money2 = new Money(-30m, "USD");

        // Act
        var result = money1.Add(money2);

        // Assert
        result.Amount.Should().Be(70m);
    }

    [Fact]
    public void Add_DifferentCurrency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var vnd = new Money(100_000m, "VND");
        var usd = new Money(5m, "USD");

        // Act
        var action = () => vnd.Add(usd);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    #endregion

    #region Subtract

    [Fact]
    public void Subtract_SameCurrency_ShouldReturnDifference()
    {
        // Arrange
        var money1 = new Money(100_000m, "VND");
        var money2 = new Money(30_000m, "VND");

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.Amount.Should().Be(70_000m);
        result.Currency.Should().Be("VND");
    }

    [Fact]
    public void Subtract_ResultNegative_ShouldSucceed()
    {
        // Arrange
        var money1 = new Money(10m, "USD");
        var money2 = new Money(25m, "USD");

        // Act
        var result = money1.Subtract(money2);

        // Assert
        result.Amount.Should().Be(-15m);
    }

    [Fact]
    public void Subtract_DifferentCurrency_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var vnd = new Money(100_000m, "VND");
        var usd = new Money(5m, "USD");

        // Act
        var action = () => vnd.Subtract(usd);

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*different currencies*");
    }

    #endregion

    #region Equals and GetHashCode

    [Fact]
    public void Equals_SameAmountAndCurrency_ShouldBeTrue()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(100m, "VND");

        // Act & Assert
        money1.Equals(money2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentAmount_ShouldBeFalse()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(200m, "VND");

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
    }

    [Fact]
    public void Equals_DifferentCurrency_ShouldBeFalse()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(100m, "USD");

        // Act & Assert
        money1.Equals(money2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        // Arrange
        var money = new Money(100m, "VND");

        // Act & Assert
        money.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_SameValue_ShouldBeTrue()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        object money2 = new Money(100m, "VND");

        // Act & Assert
        money1.Equals(money2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ShouldBeFalse()
    {
        // Arrange
        var money = new Money(100m, "VND");

        // Act & Assert
        money.Equals("not a money").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameAmountAndCurrency_ShouldBeEqual()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(100m, "VND");

        // Act & Assert
        money1.GetHashCode().Should().Be(money2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValues_ShouldLikelyDiffer()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(200m, "VND");
        var money3 = new Money(100m, "USD");

        // Act & Assert
        money1.GetHashCode().Should().NotBe(money2.GetHashCode());
        money1.GetHashCode().Should().NotBe(money3.GetHashCode());
    }

    #endregion

    #region ConvertTo

    [Fact]
    public void ConvertTo_ValidRate_ShouldConvertCorrectly()
    {
        // Arrange
        var vnd = new Money(1_000_000m, "VND");

        // Act
        var usd = vnd.ConvertTo("USD", 0.00004m);

        // Assert
        usd.Amount.Should().Be(40m);
        usd.Currency.Should().Be("USD");
    }

    [Fact]
    public void ConvertTo_ShouldRoundToTwoDecimals()
    {
        // Arrange
        var money = new Money(100m, "VND");

        // Act
        var result = money.ConvertTo("USD", 0.333333m);

        // Assert
        result.Amount.Should().Be(33.33m);
    }

    [Fact]
    public void ConvertTo_ZeroRate_ShouldThrowArgumentException()
    {
        // Arrange
        var money = new Money(100m, "VND");

        // Act
        var action = () => money.ConvertTo("USD", 0m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("rate");
    }

    [Fact]
    public void ConvertTo_NegativeRate_ShouldThrowArgumentException()
    {
        // Arrange
        var money = new Money(100m, "VND");

        // Act
        var action = () => money.ConvertTo("USD", -1m);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("rate");
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldFormatWithTwoDecimalsAndCurrency()
    {
        // Arrange
        var money = new Money(1_234_567m, "VND");

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Contain("VND");
        result.Should().Contain("1");
        result.Should().Contain("234");
        result.Should().Contain("567");
    }

    [Fact]
    public void ToString_ZeroAmount_ShouldFormat()
    {
        // Arrange
        var money = new Money(0m, "USD");

        // Act
        var result = money.ToString();

        // Assert
        result.Should().Be("0.00 USD");
    }

    #endregion

    #region Operators

    [Fact]
    public void EqualityOperator_SameValues_ShouldReturnTrue()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(100m, "VND");

        // Act & Assert
        (money1 == money2).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentValues_ShouldReturnFalse()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(200m, "VND");

        // Act & Assert
        (money1 == money2).Should().BeFalse();
    }

    [Fact]
    public void InequalityOperator_DifferentValues_ShouldReturnTrue()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(200m, "VND");

        // Act & Assert
        (money1 != money2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValues_ShouldReturnFalse()
    {
        // Arrange
        var money1 = new Money(100m, "VND");
        var money2 = new Money(100m, "VND");

        // Act & Assert
        (money1 != money2).Should().BeFalse();
    }

    #endregion
}

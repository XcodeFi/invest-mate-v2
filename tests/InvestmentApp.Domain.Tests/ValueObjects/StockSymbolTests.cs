using FluentAssertions;
using InvestmentApp.Domain.ValueObjects;

namespace InvestmentApp.Domain.Tests.ValueObjects;

public class StockSymbolTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidSymbol_ShouldCreateStockSymbol()
    {
        // Act
        var symbol = new StockSymbol("VNM");

        // Assert
        symbol.Value.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_LowercaseInput_ShouldNormalizeToUppercase()
    {
        // Act
        var symbol = new StockSymbol("vnm");

        // Assert
        symbol.Value.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_MixedCaseInput_ShouldNormalizeToUppercase()
    {
        // Act
        var symbol = new StockSymbol("fPt");

        // Assert
        symbol.Value.Should().Be("FPT");
    }

    [Fact]
    public void Constructor_InputWithLeadingAndTrailingSpaces_ShouldTrim()
    {
        // Act
        var symbol = new StockSymbol("  VNM  ");

        // Assert
        symbol.Value.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_InputWithSpacesAndLowercase_ShouldNormalizeAndTrim()
    {
        // Act
        var symbol = new StockSymbol("  vnm  ");

        // Assert
        symbol.Value.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_NullValue_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockSymbol(null!);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Constructor_EmptyString_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockSymbol("");

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    [Fact]
    public void Constructor_WhitespaceOnly_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockSymbol("   ");

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("value");
    }

    #endregion

    #region Equals and GetHashCode

    [Fact]
    public void Equals_SameValue_ShouldBeTrue()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        symbol1.Equals(symbol2).Should().BeTrue();
    }

    [Fact]
    public void Equals_SameValueDifferentCase_ShouldBeTrue()
    {
        // Arrange
        var symbol1 = new StockSymbol("vnm");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        symbol1.Equals(symbol2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ShouldBeFalse()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("FPT");

        // Act & Assert
        symbol1.Equals(symbol2).Should().BeFalse();
    }

    [Fact]
    public void Equals_Null_ShouldBeFalse()
    {
        // Arrange
        var symbol = new StockSymbol("VNM");

        // Act & Assert
        symbol.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equals_ObjectOverload_SameValue_ShouldBeTrue()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        object symbol2 = new StockSymbol("VNM");

        // Act & Assert
        symbol1.Equals(symbol2).Should().BeTrue();
    }

    [Fact]
    public void Equals_ObjectOverload_DifferentType_ShouldBeFalse()
    {
        // Arrange
        var symbol = new StockSymbol("VNM");

        // Act & Assert
        symbol.Equals("VNM").Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_SameValue_ShouldBeEqual()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        symbol1.GetHashCode().Should().Be(symbol2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_NormalizedSameValue_ShouldBeEqual()
    {
        // Arrange
        var symbol1 = new StockSymbol("vnm");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        symbol1.GetHashCode().Should().Be(symbol2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentValue_ShouldLikelyDiffer()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("FPT");

        // Act & Assert
        symbol1.GetHashCode().Should().NotBe(symbol2.GetHashCode());
    }

    #endregion

    #region ToString

    [Fact]
    public void ToString_ShouldReturnNormalizedValue()
    {
        // Arrange
        var symbol = new StockSymbol("vnm");

        // Act & Assert
        symbol.ToString().Should().Be("VNM");
    }

    #endregion

    #region Operators

    [Fact]
    public void EqualityOperator_SameValue_ShouldReturnTrue()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        (symbol1 == symbol2).Should().BeTrue();
    }

    [Fact]
    public void EqualityOperator_DifferentValue_ShouldReturnFalse()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("FPT");

        // Act & Assert
        (symbol1 == symbol2).Should().BeFalse();
    }

    [Fact]
    public void InequalityOperator_DifferentValue_ShouldReturnTrue()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("FPT");

        // Act & Assert
        (symbol1 != symbol2).Should().BeTrue();
    }

    [Fact]
    public void InequalityOperator_SameValue_ShouldReturnFalse()
    {
        // Arrange
        var symbol1 = new StockSymbol("VNM");
        var symbol2 = new StockSymbol("VNM");

        // Act & Assert
        (symbol1 != symbol2).Should().BeFalse();
    }

    #endregion

    #region Implicit and Explicit Operators

    [Fact]
    public void ImplicitStringConversion_ShouldReturnValue()
    {
        // Arrange
        var symbol = new StockSymbol("VNM");

        // Act
        string result = symbol;

        // Assert
        result.Should().Be("VNM");
    }

    [Fact]
    public void ImplicitStringConversion_CanBeUsedInStringContext()
    {
        // Arrange
        var symbol = new StockSymbol("FPT");

        // Act
        var message = $"Symbol is {(string)symbol}";

        // Assert
        message.Should().Be("Symbol is FPT");
    }

    [Fact]
    public void ExplicitStockSymbolConversion_FromString_ShouldCreateSymbol()
    {
        // Act
        var symbol = (StockSymbol)"vnm";

        // Assert
        symbol.Value.Should().Be("VNM");
    }

    [Fact]
    public void ExplicitStockSymbolConversion_FromString_ShouldNormalize()
    {
        // Act
        var symbol = (StockSymbol)"  fpt  ";

        // Assert
        symbol.Value.Should().Be("FPT");
    }

    [Fact]
    public void ExplicitStockSymbolConversion_EmptyString_ShouldThrow()
    {
        // Act
        var action = () => (StockSymbol)"";

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion
}

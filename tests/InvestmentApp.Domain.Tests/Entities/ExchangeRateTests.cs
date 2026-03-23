using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class ExchangeRateTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateExchangeRate()
    {
        // Arrange
        var baseCurrency = "USD";
        var targetCurrency = "VND";
        var rate = 25_400m;
        var date = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);

        // Act
        var exchangeRate = new ExchangeRate(baseCurrency, targetCurrency, rate, date);

        // Assert
        exchangeRate.Id.Should().NotBeNullOrEmpty();
        exchangeRate.BaseCurrency.Should().Be("USD");
        exchangeRate.TargetCurrency.Should().Be("VND");
        exchangeRate.Rate.Should().Be(25_400m);
        exchangeRate.Source.Should().Be("manual");
        exchangeRate.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_CustomSource_ShouldUseProvidedSource()
    {
        // Act
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m,
            new DateTime(2025, 6, 15), "api-exchange");

        // Assert
        exchangeRate.Source.Should().Be("api-exchange");
    }

    #endregion

    #region Constructor — Currency Normalization

    [Fact]
    public void Constructor_LowercaseBaseCurrency_ShouldConvertToUpperInvariant()
    {
        // Act
        var exchangeRate = new ExchangeRate("usd", "VND", 25_400m, new DateTime(2025, 6, 15));

        // Assert
        exchangeRate.BaseCurrency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_LowercaseTargetCurrency_ShouldConvertToUpperInvariant()
    {
        // Act
        var exchangeRate = new ExchangeRate("USD", "vnd", 25_400m, new DateTime(2025, 6, 15));

        // Assert
        exchangeRate.TargetCurrency.Should().Be("VND");
    }

    [Fact]
    public void Constructor_MixedCaseCurrencies_ShouldConvertToUpperInvariant()
    {
        // Act
        var exchangeRate = new ExchangeRate("Usd", "Vnd", 25_400m, new DateTime(2025, 6, 15));

        // Assert
        exchangeRate.BaseCurrency.Should().Be("USD");
        exchangeRate.TargetCurrency.Should().Be("VND");
    }

    #endregion

    #region Constructor — Date Normalization

    [Fact]
    public void Constructor_DateWithTime_ShouldNormalizeToDateOnly()
    {
        // Arrange
        var dateWithTime = new DateTime(2025, 6, 15, 14, 30, 45, DateTimeKind.Utc);

        // Act
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m, dateWithTime);

        // Assert
        exchangeRate.Date.Should().Be(new DateTime(2025, 6, 15));
        exchangeRate.Date.Hour.Should().Be(0);
        exchangeRate.Date.Minute.Should().Be(0);
        exchangeRate.Date.Second.Should().Be(0);
    }

    [Fact]
    public void Constructor_DateAtMidnight_ShouldRemainUnchanged()
    {
        // Arrange
        var dateAtMidnight = new DateTime(2025, 6, 15, 0, 0, 0);

        // Act
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m, dateAtMidnight);

        // Assert
        exchangeRate.Date.Should().Be(dateAtMidnight);
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullBaseCurrency_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new ExchangeRate(null!, "VND", 25_400m, new DateTime(2025, 6, 15));

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("baseCurrency");
    }

    [Fact]
    public void Constructor_NullTargetCurrency_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new ExchangeRate("USD", null!, 25_400m, new DateTime(2025, 6, 15));

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("targetCurrency");
    }

    [Fact]
    public void Constructor_ZeroRate_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new ExchangeRate("USD", "VND", 0m, new DateTime(2025, 6, 15));

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("rate");
    }

    [Fact]
    public void Constructor_NegativeRate_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new ExchangeRate("USD", "VND", -100m, new DateTime(2025, 6, 15));

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("rate");
    }

    #endregion

    #region UpdateRate — Valid

    [Fact]
    public void UpdateRate_ValidRate_ShouldUpdateRateAndSource()
    {
        // Arrange
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m,
            new DateTime(2025, 6, 15), "manual");

        // Act
        exchangeRate.UpdateRate(25_500m, "api-exchange");

        // Assert
        exchangeRate.Rate.Should().Be(25_500m);
        exchangeRate.Source.Should().Be("api-exchange");
    }

    [Fact]
    public void UpdateRate_ShouldUpdateTimestampAndIncrementVersion()
    {
        // Arrange
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m,
            new DateTime(2025, 6, 15));
        var initialVersion = exchangeRate.Version;

        // Act
        exchangeRate.UpdateRate(25_500m, "api");

        // Assert
        exchangeRate.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        exchangeRate.Version.Should().Be(initialVersion + 1);
    }

    #endregion

    #region UpdateRate — Invalid

    [Fact]
    public void UpdateRate_ZeroRate_ShouldThrowArgumentException()
    {
        // Arrange
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m, new DateTime(2025, 6, 15));

        // Act
        var action = () => exchangeRate.UpdateRate(0m, "api");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateRate_NegativeRate_ShouldThrowArgumentException()
    {
        // Arrange
        var exchangeRate = new ExchangeRate("USD", "VND", 25_400m, new DateTime(2025, 6, 15));

        // Act
        var action = () => exchangeRate.UpdateRate(-100m, "api");

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion
}

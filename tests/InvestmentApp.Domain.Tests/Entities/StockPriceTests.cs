using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class StockPriceTests
{
    #region Constructor — Valid Cases

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateStockPrice()
    {
        // Arrange
        var symbol = "VNM";
        var date = new DateTime(2025, 6, 15, 14, 30, 0, DateTimeKind.Utc);
        var open = 80000m;
        var high = 85000m;
        var low = 79000m;
        var close = 84000m;
        var volume = 1_500_000L;

        // Act
        var stockPrice = new StockPrice(symbol, date, open, high, low, close, volume);

        // Assert
        stockPrice.Id.Should().NotBeNullOrEmpty();
        stockPrice.Symbol.Should().Be("VNM");
        stockPrice.Date.Should().Be(new DateTime(2025, 6, 15)); // Normalized to .Date
        stockPrice.Open.Should().Be(open);
        stockPrice.High.Should().Be(high);
        stockPrice.Low.Should().Be(low);
        stockPrice.Close.Should().Be(close);
        stockPrice.Volume.Should().Be(volume);
        stockPrice.Source.Should().Be("Manual");
        stockPrice.FetchedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Constructor_CustomSource_ShouldUseProvidedSource()
    {
        // Act
        var stockPrice = new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L, "SSI-API");

        // Assert
        stockPrice.Source.Should().Be("SSI-API");
    }

    [Fact]
    public void Constructor_ZeroPrices_ShouldBeAllowed()
    {
        // Act
        var stockPrice = new StockPrice("VNM", new DateTime(2025, 6, 15),
            0m, 0m, 0m, 0m, 0L);

        // Assert
        stockPrice.Open.Should().Be(0m);
        stockPrice.High.Should().Be(0m);
        stockPrice.Low.Should().Be(0m);
        stockPrice.Close.Should().Be(0m);
        stockPrice.Volume.Should().Be(0L);
    }

    #endregion

    #region Constructor — Symbol Normalization

    [Fact]
    public void Constructor_LowercaseSymbol_ShouldConvertToUpperCase()
    {
        // Act
        var stockPrice = new StockPrice("vnm", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Assert
        stockPrice.Symbol.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_MixedCaseSymbol_ShouldConvertToUpperCase()
    {
        // Act
        var stockPrice = new StockPrice("Fpt", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Assert
        stockPrice.Symbol.Should().Be("FPT");
    }

    [Fact]
    public void Constructor_SymbolWithWhitespace_ShouldTrimAndUpperCase()
    {
        // Act
        var stockPrice = new StockPrice("  vnm  ", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Assert
        stockPrice.Symbol.Should().Be("VNM");
    }

    #endregion

    #region Constructor — Validation Failures

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new StockPrice(null!, new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_NegativeOpen_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockPrice("VNM", new DateTime(2025, 6, 15),
            -1m, 85000m, 79000m, 84000m, 1_500_000L);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("open");
    }

    [Fact]
    public void Constructor_NegativeHigh_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, -1m, 79000m, 84000m, 1_500_000L);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("high");
    }

    [Fact]
    public void Constructor_NegativeLow_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, -1m, 84000m, 1_500_000L);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("low");
    }

    [Fact]
    public void Constructor_NegativeClose_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, -1m, 1_500_000L);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("close");
    }

    [Fact]
    public void Constructor_NegativeVolume_ShouldThrowArgumentException()
    {
        // Act
        var action = () => new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, -1L);

        // Assert
        action.Should().Throw<ArgumentException>().WithParameterName("volume");
    }

    #endregion

    #region UpdatePrice

    [Fact]
    public void UpdatePrice_ShouldUpdateAllPriceFields()
    {
        // Arrange
        var stockPrice = new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Act
        stockPrice.UpdatePrice(81000m, 86000m, 80000m, 85000m, 2_000_000L);

        // Assert
        stockPrice.Open.Should().Be(81000m);
        stockPrice.High.Should().Be(86000m);
        stockPrice.Low.Should().Be(80000m);
        stockPrice.Close.Should().Be(85000m);
        stockPrice.Volume.Should().Be(2_000_000L);
    }

    [Fact]
    public void UpdatePrice_ShouldUpdateFetchedAt()
    {
        // Arrange
        var stockPrice = new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);

        // Act
        stockPrice.UpdatePrice(81000m, 86000m, 80000m, 85000m, 2_000_000L);

        // Assert
        stockPrice.FetchedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UpdatePrice_ShouldNotChangeSymbolOrDate()
    {
        // Arrange
        var stockPrice = new StockPrice("VNM", new DateTime(2025, 6, 15),
            80000m, 85000m, 79000m, 84000m, 1_500_000L);
        var originalSymbol = stockPrice.Symbol;
        var originalDate = stockPrice.Date;

        // Act
        stockPrice.UpdatePrice(81000m, 86000m, 80000m, 85000m, 2_000_000L);

        // Assert
        stockPrice.Symbol.Should().Be(originalSymbol);
        stockPrice.Date.Should().Be(originalDate);
    }

    #endregion
}

using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class MarketIndexTests
{
    #region Constructor

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateMarketIndex()
    {
        // Arrange
        var indexSymbol = "VNINDEX";
        var date = new DateTime(2026, 3, 15, 10, 30, 0);
        var open = 1200m;
        var high = 1250m;
        var low = 1190m;
        var close = 1230m;
        var volume = 500_000_000L;

        // Act
        var mi = new MarketIndex(indexSymbol, date, open, high, low, close, volume);

        // Assert
        mi.Id.Should().NotBeNullOrEmpty();
        mi.IndexSymbol.Should().Be("VNINDEX");
        mi.Date.Should().Be(new DateTime(2026, 3, 15)); // normalized to .Date
        mi.Open.Should().Be(open);
        mi.High.Should().Be(high);
        mi.Low.Should().Be(low);
        mi.Close.Should().Be(close);
        mi.Volume.Should().Be(volume);
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrowArgumentNullException()
    {
        // Act
        var action = () => new MarketIndex(null!, DateTime.UtcNow, 100m, 110m, 90m, 105m, 1000L);

        // Assert
        action.Should().Throw<ArgumentNullException>().WithParameterName("indexSymbol");
    }

    [Fact]
    public void Constructor_SymbolNormalization_ShouldUpperCaseAndTrim()
    {
        // Act
        var mi = new MarketIndex("  vn30  ", DateTime.UtcNow, 100m, 110m, 90m, 105m, 1000L);

        // Assert
        mi.IndexSymbol.Should().Be("VN30");
    }

    [Fact]
    public void Constructor_LowercaseSymbol_ShouldNormalizeToUpperCase()
    {
        // Act
        var mi = new MarketIndex("vnindex", DateTime.UtcNow, 100m, 110m, 90m, 105m, 1000L);

        // Assert
        mi.IndexSymbol.Should().Be("VNINDEX");
    }

    [Fact]
    public void Constructor_DateNormalization_ShouldStripTimeComponent()
    {
        // Arrange
        var dateWithTime = new DateTime(2026, 3, 15, 14, 30, 45, 123);

        // Act
        var mi = new MarketIndex("VNINDEX", dateWithTime, 100m, 110m, 90m, 105m, 1000L);

        // Assert
        mi.Date.Should().Be(new DateTime(2026, 3, 15));
        mi.Date.Hour.Should().Be(0);
        mi.Date.Minute.Should().Be(0);
        mi.Date.Second.Should().Be(0);
    }

    #endregion

    #region Change Calculation

    [Fact]
    public void Constructor_ShouldCalculateChange()
    {
        // Arrange — close=1230, open=1200 → Change = 30
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1230m, 1000L);

        // Assert
        mi.Change.Should().Be(30m);
    }

    [Fact]
    public void Constructor_CloseEqualsOpen_ShouldHaveZeroChange()
    {
        // Act
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1200m, 1000L);

        // Assert
        mi.Change.Should().Be(0m);
    }

    [Fact]
    public void Constructor_CloseBelowOpen_ShouldHaveNegativeChange()
    {
        // Arrange — close=1180, open=1200 → Change = -20
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1180m, 1180m, 1000L);

        // Assert
        mi.Change.Should().Be(-20m);
    }

    #endregion

    #region ChangePercent Calculation

    [Fact]
    public void Constructor_ShouldCalculateChangePercent()
    {
        // Arrange — close=1230, open=1200 → ChangePercent = (30/1200)*100 = 2.5
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1230m, 1000L);

        // Assert
        mi.ChangePercent.Should().Be(2.5m);
    }

    [Fact]
    public void Constructor_OpenIsZero_ShouldReturnZeroChangePercent()
    {
        // Act
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 0m, 110m, 0m, 105m, 1000L);

        // Assert
        mi.ChangePercent.Should().Be(0m);
    }

    [Fact]
    public void Constructor_NegativeChangePercent_ShouldBeNegative()
    {
        // Arrange — close=1176, open=1200 → ChangePercent = (-24/1200)*100 = -2.0
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1170m, 1176m, 1000L);

        // Assert
        mi.ChangePercent.Should().Be(-2m);
    }

    #endregion

    #region UpdateData

    [Fact]
    public void UpdateData_ShouldUpdateAllFieldsAndRecalculate()
    {
        // Arrange
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1230m, 1000L);

        // Act — new data: open=1300, close=1350 → Change=50, ChangePercent = (50/1300)*100
        mi.UpdateData(1300m, 1400m, 1280m, 1350m, 2000L);

        // Assert
        mi.Open.Should().Be(1300m);
        mi.High.Should().Be(1400m);
        mi.Low.Should().Be(1280m);
        mi.Close.Should().Be(1350m);
        mi.Volume.Should().Be(2000L);
        mi.Change.Should().Be(50m);

        var expectedChangePercent = (50m / 1300m) * 100m;
        mi.ChangePercent.Should().Be(expectedChangePercent);
    }

    [Fact]
    public void UpdateData_OpenIsZero_ShouldSetChangePercentToZero()
    {
        // Arrange
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1230m, 1000L);

        // Act
        mi.UpdateData(0m, 110m, 0m, 105m, 500L);

        // Assert
        mi.Change.Should().Be(105m);
        mi.ChangePercent.Should().Be(0m);
    }

    [Fact]
    public void UpdateData_ShouldRecalculateNegativeChange()
    {
        // Arrange
        var mi = new MarketIndex("VNINDEX", DateTime.UtcNow, 1200m, 1250m, 1190m, 1230m, 1000L);

        // Act — open=1000, close=950 → Change=-50, ChangePercent = -5
        mi.UpdateData(1000m, 1050m, 940m, 950m, 3000L);

        // Assert
        mi.Change.Should().Be(-50m);
        mi.ChangePercent.Should().Be(-5m);
    }

    #endregion
}

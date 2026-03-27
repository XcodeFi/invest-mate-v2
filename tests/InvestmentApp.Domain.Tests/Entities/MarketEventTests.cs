using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class MarketEventTests
{
    [Fact]
    public void Constructor_ValidParameters_ShouldCreateMarketEvent()
    {
        var eventDate = new DateTime(2024, 4, 2, 0, 0, 0, DateTimeKind.Utc);

        var evt = new MarketEvent(
            symbol: "VNM",
            eventType: MarketEventType.Earnings,
            title: "KQKD Q1 2024 — Doanh thu +12%",
            eventDate: eventDate);

        evt.Id.Should().NotBeNullOrEmpty();
        evt.Symbol.Should().Be("VNM");
        evt.EventType.Should().Be(MarketEventType.Earnings);
        evt.Title.Should().Be("KQKD Q1 2024 — Doanh thu +12%");
        evt.EventDate.Should().Be(eventDate);
        evt.Description.Should().BeNull();
        evt.Source.Should().BeNull();
        evt.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_WithOptionalFields_ShouldSetAll()
    {
        var evt = new MarketEvent(
            symbol: "VNM",
            eventType: MarketEventType.Dividend,
            title: "Cổ tức 2024",
            eventDate: DateTime.UtcNow,
            description: "Cổ tức tiền mặt 1,500đ/cp",
            source: "https://cafef.vn/vnm-co-tuc");

        evt.Description.Should().Be("Cổ tức tiền mặt 1,500đ/cp");
        evt.Source.Should().Be("https://cafef.vn/vnm-co-tuc");
    }

    [Fact]
    public void Constructor_SymbolShouldBeUpperTrimmed()
    {
        var evt = new MarketEvent(" vnm ", MarketEventType.News, "T", DateTime.UtcNow);

        evt.Symbol.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrow()
    {
        var action = () => new MarketEvent(null!, MarketEventType.News, "T", DateTime.UtcNow);
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_EmptySymbol_ShouldThrow()
    {
        var action = () => new MarketEvent("  ", MarketEventType.News, "T", DateTime.UtcNow);
        action.Should().Throw<ArgumentException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_NullTitle_ShouldThrow()
    {
        var action = () => new MarketEvent("VNM", MarketEventType.News, null!, DateTime.UtcNow);
        action.Should().Throw<ArgumentNullException>().WithParameterName("title");
    }

    [Theory]
    [InlineData(MarketEventType.Earnings)]
    [InlineData(MarketEventType.Dividend)]
    [InlineData(MarketEventType.RightsIssue)]
    [InlineData(MarketEventType.ShareholderMtg)]
    [InlineData(MarketEventType.InsiderTrade)]
    [InlineData(MarketEventType.News)]
    [InlineData(MarketEventType.Macro)]
    public void Constructor_AllEventTypes_ShouldBeAccepted(MarketEventType eventType)
    {
        var evt = new MarketEvent("VNM", eventType, "Test", DateTime.UtcNow);
        evt.EventType.Should().Be(eventType);
    }

    [Fact]
    public void Update_ShouldUpdateFields()
    {
        var evt = new MarketEvent("VNM", MarketEventType.News, "Old title", DateTime.UtcNow);

        evt.Update(title: "New title", description: "Updated desc", source: "https://new.source");

        evt.Title.Should().Be("New title");
        evt.Description.Should().Be("Updated desc");
        evt.Source.Should().Be("https://new.source");
    }

    [Fact]
    public void Update_NullFields_ShouldNotOverwrite()
    {
        var evt = new MarketEvent("VNM", MarketEventType.News, "Keep this",
            DateTime.UtcNow, description: "Keep desc");

        evt.Update(title: null, description: null);

        evt.Title.Should().Be("Keep this");
        evt.Description.Should().Be("Keep desc");
    }
}

using FluentAssertions;
using InvestmentApp.Domain.Entities;

namespace InvestmentApp.Domain.Tests.Entities;

public class JournalEntryTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_ValidParameters_ShouldCreateJournalEntry()
    {
        // Arrange & Act
        var entry = new JournalEntry(
            userId: "user-1",
            symbol: "VNM",
            entryType: JournalEntryType.Observation,
            title: "RSI oversold",
            content: "RSI giảm xuống 28, test hỗ trợ 72k");

        // Assert
        entry.Id.Should().NotBeNullOrEmpty();
        entry.UserId.Should().Be("user-1");
        entry.Symbol.Should().Be("VNM");
        entry.EntryType.Should().Be(JournalEntryType.Observation);
        entry.Title.Should().Be("RSI oversold");
        entry.Content.Should().Be("RSI giảm xuống 28, test hỗ trợ 72k");
        entry.PortfolioId.Should().BeNull();
        entry.TradeId.Should().BeNull();
        entry.TradePlanId.Should().BeNull();
        entry.EmotionalState.Should().BeNull();
        entry.ConfidenceLevel.Should().BeNull();
        entry.PriceAtTime.Should().BeNull();
        entry.VnIndexAtTime.Should().BeNull();
        entry.MarketContext.Should().BeNull();
        entry.Rating.Should().BeNull();
        entry.Tags.Should().BeEmpty();
        entry.IsDeleted.Should().BeFalse();
        entry.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entry.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        entry.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Constructor_SymbolShouldBeUpperTrimmed()
    {
        var entry = new JournalEntry("user-1", " vnm ", JournalEntryType.Observation, "Test", "Content");

        entry.Symbol.Should().Be("VNM");
    }

    [Fact]
    public void Constructor_NullUserId_ShouldThrow()
    {
        var action = () => new JournalEntry(null!, "VNM", JournalEntryType.Observation, "T", "C");
        action.Should().Throw<ArgumentNullException>().WithParameterName("userId");
    }

    [Fact]
    public void Constructor_NullSymbol_ShouldThrow()
    {
        var action = () => new JournalEntry("user-1", null!, JournalEntryType.Observation, "T", "C");
        action.Should().Throw<ArgumentNullException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_EmptySymbol_ShouldThrow()
    {
        var action = () => new JournalEntry("user-1", "  ", JournalEntryType.Observation, "T", "C");
        action.Should().Throw<ArgumentException>().WithParameterName("symbol");
    }

    [Fact]
    public void Constructor_NullTitle_ShouldThrow()
    {
        var action = () => new JournalEntry("user-1", "VNM", JournalEntryType.Observation, null!, "C");
        action.Should().Throw<ArgumentNullException>().WithParameterName("title");
    }

    [Fact]
    public void Constructor_WithOptionalParameters_ShouldSetAll()
    {
        var timestamp = new DateTime(2024, 3, 15, 9, 30, 0, DateTimeKind.Utc);

        var entry = new JournalEntry(
            userId: "user-1",
            symbol: "VNM",
            entryType: JournalEntryType.PreTrade,
            title: "Phân tích trước GD",
            content: "RSI bounce, hỗ trợ tốt",
            portfolioId: "portfolio-1",
            tradeId: "trade-1",
            tradePlanId: "plan-1",
            emotionalState: "Tự tin",
            confidenceLevel: 8,
            priceAtTime: 72_000m,
            vnIndexAtTime: 1_250.5m,
            marketContext: "VNI sideway",
            tags: new List<string> { "RSI", "hỗ trợ" },
            timestamp: timestamp);

        entry.PortfolioId.Should().Be("portfolio-1");
        entry.TradeId.Should().Be("trade-1");
        entry.TradePlanId.Should().Be("plan-1");
        entry.EmotionalState.Should().Be("Tự tin");
        entry.ConfidenceLevel.Should().Be(8);
        entry.PriceAtTime.Should().Be(72_000m);
        entry.VnIndexAtTime.Should().Be(1_250.5m);
        entry.MarketContext.Should().Be("VNI sideway");
        entry.Tags.Should().BeEquivalentTo(new[] { "RSI", "hỗ trợ" });
        entry.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region ConfidenceLevel Clamping

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(11, 10)]
    [InlineData(100, 10)]
    [InlineData(5, 5)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    public void Constructor_ConfidenceLevel_ShouldClamp1To10(int input, int expected)
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T", "C",
            confidenceLevel: input);

        entry.ConfidenceLevel.Should().Be(expected);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_ShouldUpdateProvidedFields()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "Old", "Old content");
        var oldVersion = entry.Version;

        entry.Update(
            title: "New title",
            content: "New content",
            emotionalState: "Bình tĩnh",
            confidenceLevel: 7,
            marketContext: "Bull market",
            tags: new List<string> { "updated" });

        entry.Title.Should().Be("New title");
        entry.Content.Should().Be("New content");
        entry.EmotionalState.Should().Be("Bình tĩnh");
        entry.ConfidenceLevel.Should().Be(7);
        entry.MarketContext.Should().Be("Bull market");
        entry.Tags.Should().Contain("updated");
        entry.Version.Should().Be(oldVersion + 1);
        entry.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Update_NullFields_ShouldNotOverwrite()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation,
            "Original", "Original content", emotionalState: "Tự tin", confidenceLevel: 8);

        entry.Update(title: null, content: null, emotionalState: null, confidenceLevel: null);

        entry.Title.Should().Be("Original");
        entry.Content.Should().Be("Original content");
        entry.EmotionalState.Should().Be("Tự tin");
        entry.ConfidenceLevel.Should().Be(8);
    }

    [Fact]
    public void Update_ConfidenceLevel_ShouldClamp()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T", "C");

        entry.Update(confidenceLevel: 15);
        entry.ConfidenceLevel.Should().Be(10);

        entry.Update(confidenceLevel: -3);
        entry.ConfidenceLevel.Should().Be(1);
    }

    #endregion

    #region Rating Tests

    [Fact]
    public void SetRating_ValidRange_ShouldSetRating()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Review, "Review", "Content");

        entry.SetRating(4);

        entry.Rating.Should().Be(4);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(6, 5)]
    [InlineData(0, 0)]
    [InlineData(5, 5)]
    [InlineData(3, 3)]
    public void SetRating_ShouldClamp0To5(int input, int expected)
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Review, "Review", "C");

        entry.SetRating(input);

        entry.Rating.Should().Be(expected);
    }

    #endregion

    #region SoftDelete Tests

    [Fact]
    public void SoftDelete_ShouldSetIsDeletedTrue()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T", "C");

        entry.SoftDelete();

        entry.IsDeleted.Should().BeTrue();
        entry.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region SetPriceSnapshot Tests

    [Fact]
    public void SetPriceSnapshot_ShouldUpdatePrices()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T", "C");

        entry.SetPriceSnapshot(72_000m, 1_250.5m);

        entry.PriceAtTime.Should().Be(72_000m);
        entry.VnIndexAtTime.Should().Be(1_250.5m);
    }

    #endregion

    #region EntryType Enum Tests

    [Theory]
    [InlineData(JournalEntryType.Observation)]
    [InlineData(JournalEntryType.PreTrade)]
    [InlineData(JournalEntryType.DuringTrade)]
    [InlineData(JournalEntryType.PostTrade)]
    [InlineData(JournalEntryType.Review)]
    public void Constructor_AllEntryTypes_ShouldBeAccepted(JournalEntryType entryType)
    {
        var entry = new JournalEntry("user-1", "VNM", entryType, "T", "C");

        entry.EntryType.Should().Be(entryType);
    }

    #endregion

    #region LinkTrade / LinkTradePlan Tests

    [Fact]
    public void LinkTrade_ShouldSetTradeId()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.PostTrade, "T", "C");

        entry.LinkTrade("trade-123");

        entry.TradeId.Should().Be("trade-123");
    }

    [Fact]
    public void LinkTradePlan_ShouldSetTradePlanId()
    {
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "T", "C");

        entry.LinkTradePlan("plan-456");

        entry.TradePlanId.Should().Be("plan-456");
    }

    #endregion
}

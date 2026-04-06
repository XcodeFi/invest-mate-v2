using FluentAssertions;
using InvestmentApp.Domain.Entities;
using InvestmentApp.Infrastructure.Services;

namespace InvestmentApp.Infrastructure.Tests.Services;

public class BehavioralAnalysisServiceTests
{
    private readonly BehavioralAnalysisService _service = new();

    #region FOMO Detection

    [Fact]
    public void DetectPatterns_FomoJournalThenBuy_DetectsFomo()
    {
        // Arrange
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var journal = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Phải mua ngay", "C",
            emotionalState: "FOMO", confidenceLevel: 3, timestamp: baseTime);
        var buy = new Trade("port-1", "VNM", TradeType.BUY, 500, 80_000m, 0, 0,
            baseTime.AddHours(2));

        // Act
        var patterns = _service.DetectPatterns(new[] { journal }, new[] { buy });

        // Assert
        patterns.Should().HaveCount(1);
        patterns[0].PatternType.Should().Be("FOMO");
        patterns[0].Severity.Should().Be("Warning");
        patterns[0].RelatedTradeId.Should().Be(buy.Id);
        patterns[0].RelatedJournalId.Should().Be(journal.Id);
    }

    [Fact]
    public void DetectPatterns_FomoJournalBuyAfter24h_NoDetection()
    {
        // Arrange — BUY too late (> 24h)
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var journal = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Phải mua", "C",
            emotionalState: "FOMO", timestamp: baseTime);
        var buy = new Trade("port-1", "VNM", TradeType.BUY, 500, 80_000m, 0, 0,
            baseTime.AddHours(25));

        // Act
        var patterns = _service.DetectPatterns(new[] { journal }, new[] { buy });

        // Assert
        patterns.Should().BeEmpty();
    }

    [Fact]
    public void DetectPatterns_CalmJournalThenBuy_NoFomo()
    {
        // Arrange — Bình tĩnh is not FOMO
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var journal = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Phân tích kỹ", "C",
            emotionalState: "Bình tĩnh", timestamp: baseTime);
        var buy = new Trade("port-1", "VNM", TradeType.BUY, 500, 80_000m, 0, 0,
            baseTime.AddHours(1));

        // Act — no FOMO patterns, but also no panic/revenge
        var patterns = _service.DetectPatterns(new[] { journal }, new[] { buy });

        // Assert
        patterns.Where(p => p.PatternType == "FOMO").Should().BeEmpty();
    }

    #endregion

    #region Panic Sell Detection

    [Fact]
    public void DetectPatterns_PanicJournalThenSell_DetectsPanicSell()
    {
        // Arrange
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var journal = new JournalEntry("user-1", "VNM", JournalEntryType.DuringTrade, "Sợ quá", "C",
            emotionalState: "Sợ hãi", timestamp: baseTime);
        var sell = new Trade("port-1", "VNM", TradeType.SELL, 500, 70_000m, 0, 0,
            baseTime.AddHours(1));

        // Act
        var patterns = _service.DetectPatterns(new[] { journal }, new[] { sell });

        // Assert
        patterns.Should().HaveCount(1);
        patterns[0].PatternType.Should().Be("PanicSell");
        patterns[0].Severity.Should().Be("Critical");
    }

    #endregion

    #region Revenge Trading Detection

    [Fact]
    public void DetectPatterns_SellLossThenBuyQuickly_DetectsRevenge()
    {
        // Arrange: BUY@80k → SELL@70k (loss) → BUY within 4h
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var buy1 = new Trade("port-1", "VNM", TradeType.BUY, 500, 80_000m, 0, 0, baseTime);
        var sellLoss = new Trade("port-1", "VNM", TradeType.SELL, 500, 70_000m, 0, 0,
            baseTime.AddHours(5));
        var revengeBuy = new Trade("port-1", "VNM", TradeType.BUY, 500, 72_000m, 0, 0,
            baseTime.AddHours(7)); // 2h after loss sell

        // Act — no journal entries (no planned trade)
        var patterns = _service.DetectPatterns(
            Array.Empty<JournalEntry>(),
            new[] { buy1, sellLoss, revengeBuy });

        // Assert
        patterns.Should().ContainSingle(p => p.PatternType == "RevengeTrading");
        patterns.First(p => p.PatternType == "RevengeTrading").Severity.Should().Be("Critical");
    }

    [Fact]
    public void DetectPatterns_SellLossThenPlannedBuy_NoRevenge()
    {
        // Arrange: loss sell → BUY within 4h BUT has PreTrade journal (planned)
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var buy1 = new Trade("port-1", "VNM", TradeType.BUY, 500, 80_000m, 0, 0, baseTime);
        var sellLoss = new Trade("port-1", "VNM", TradeType.SELL, 500, 70_000m, 0, 0,
            baseTime.AddHours(5));
        var plannedBuy = new Trade("port-1", "VNM", TradeType.BUY, 500, 72_000m, 0, 0,
            baseTime.AddHours(7));
        var preTrade = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Kế hoạch mua lại", "C",
            timestamp: baseTime.AddHours(6));

        // Act
        var patterns = _service.DetectPatterns(
            new[] { preTrade },
            new[] { buy1, sellLoss, plannedBuy });

        // Assert — no revenge detected
        patterns.Where(p => p.PatternType == "RevengeTrading").Should().BeEmpty();
    }

    #endregion

    #region Overtrading Detection

    [Fact]
    public void DetectPatterns_FourBuysSameDay_DetectsOvertrading()
    {
        // Arrange
        var day = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var buys = Enumerable.Range(0, 4)
            .Select(i => new Trade("port-1", "VNM", TradeType.BUY, 100, 80_000m, 0, 0,
                day.AddHours(9 + i)))
            .ToList();

        // Act
        var patterns = _service.DetectPatterns(Array.Empty<JournalEntry>(), buys);

        // Assert
        patterns.Should().ContainSingle(p => p.PatternType == "Overtrading");
    }

    [Fact]
    public void DetectPatterns_ThreeBuysSameDay_NoOvertrading()
    {
        // Arrange — 3 is the threshold, not exceeded
        var day = new DateTime(2025, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        var buys = Enumerable.Range(0, 3)
            .Select(i => new Trade("port-1", "VNM", TradeType.BUY, 100, 80_000m, 0, 0,
                day.AddHours(9 + i)))
            .ToList();

        // Act
        var patterns = _service.DetectPatterns(Array.Empty<JournalEntry>(), buys);

        // Assert
        patterns.Where(p => p.PatternType == "Overtrading").Should().BeEmpty();
    }

    #endregion

    #region Pattern Count Summary

    [Fact]
    public void DetectPatterns_MultiplePatternsDetected_CountsCorrectly()
    {
        // Arrange — 1 FOMO + 1 Overtrading
        var baseTime = new DateTime(2025, 3, 15, 9, 0, 0, DateTimeKind.Utc);
        var fomoJournal = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "FOMO", "C",
            emotionalState: "FOMO", timestamp: baseTime);
        var buys = Enumerable.Range(0, 4)
            .Select(i => new Trade("port-1", "VNM", TradeType.BUY, 100, 80_000m, 0, 0,
                baseTime.AddHours(i)))
            .ToList();

        // Act
        var patterns = _service.DetectPatterns(new[] { fomoJournal }, buys);

        // Assert
        patterns.Where(p => p.PatternType == "FOMO").Should().HaveCount(1);
        patterns.Where(p => p.PatternType == "Overtrading").Should().HaveCount(1);
    }

    #endregion
}

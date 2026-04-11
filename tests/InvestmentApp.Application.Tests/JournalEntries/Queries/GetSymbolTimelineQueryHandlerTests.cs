using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.JournalEntries.Queries.GetSymbolTimeline;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.JournalEntries.Queries;

public class GetSymbolTimelineQueryHandlerTests
{
    private readonly Mock<IJournalEntryRepository> _journalEntryRepo;
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IMarketEventRepository> _marketEventRepo;
    private readonly Mock<IAlertHistoryRepository> _alertHistoryRepo;
    private readonly GetSymbolTimelineQueryHandler _handler;

    public GetSymbolTimelineQueryHandlerTests()
    {
        _journalEntryRepo = new Mock<IJournalEntryRepository>();
        _tradeRepo = new Mock<ITradeRepository>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _marketEventRepo = new Mock<IMarketEventRepository>();
        _alertHistoryRepo = new Mock<IAlertHistoryRepository>();
        _handler = new GetSymbolTimelineQueryHandler(
            _journalEntryRepo.Object,
            _tradeRepo.Object,
            _portfolioRepo.Object,
            _marketEventRepo.Object,
            _alertHistoryRepo.Object);
    }

    private void SetupEmptyDefaults()
    {
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry>());
        _portfolioRepo.Setup(r => r.GetByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio>());
        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade>());
        _marketEventRepo.Setup(r => r.GetBySymbolAsync(It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());
        _alertHistoryRepo.Setup(r => r.GetByUserIdAndSymbolAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());
    }

    [Fact]
    public async Task Handle_CombinesJournalTradesEventsAlerts_SortedByTimestamp()
    {
        // Arrange
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var journalEntry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation,
            "Quan sát", "Content",
            timestamp: new DateTime(2024, 3, 15, 0, 0, 0, DateTimeKind.Utc));

        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { journalEntry });

        var portfolio = new Portfolio("user-1", "My Portfolio", 50_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { portfolio });

        var trade = new Trade(portfolio.Id, "VNM", TradeType.BUY, 500, 72_000m, 0, 0,
            new DateTime(2024, 3, 18, 0, 0, 0, DateTimeKind.Utc));
        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { trade });

        var marketEvent = new MarketEvent("VNM", MarketEventType.Earnings, "KQKD Q1",
            new DateTime(2024, 4, 2, 0, 0, 0, DateTimeKind.Utc));
        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent> { marketEvent });

        _alertHistoryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Symbol.Should().Be("VNM");
        result.Items.Should().HaveCount(3);
        result.Items[0].Type.Should().Be("journal");
        result.Items[1].Type.Should().Be("trade");
        result.Items[2].Type.Should().Be("event");
        // Verify sorted by timestamp ascending
        result.Items.Should().BeInAscendingOrder(i => i.Timestamp);
    }

    [Fact]
    public async Task Handle_EmptyData_ReturnsEmptyTimeline()
    {
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.HoldingPeriods.Should().BeEmpty();
        result.EmotionSummary.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BuySellTrades_CalculatesHoldingPeriods()
    {
        SetupEmptyDefaults();

        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var portfolio = new Portfolio("user-1", "P1", 50_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { portfolio });

        var buy = new Trade(portfolio.Id, "VNM", TradeType.BUY, 500, 72_000m, 0, 0,
            new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var sell = new Trade(portfolio.Id, "VNM", TradeType.SELL, 500, 80_000m, 0, 0,
            new DateTime(2024, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { buy, sell });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.HoldingPeriods.Should().HaveCount(1);
        result.HoldingPeriods[0].StartQuantity.Should().Be(500);
        result.HoldingPeriods[0].CurrentQuantity.Should().Be(0);
        result.HoldingPeriods[0].EndDate.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_JournalEntries_CalculatesEmotionSummary()
    {
        SetupEmptyDefaults();

        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var entry1 = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
            emotionalState: "Tự tin", confidenceLevel: 8);
        var entry2 = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "T2", "C",
            emotionalState: "Tự tin", confidenceLevel: 7);
        var entry3 = new JournalEntry("user-1", "VNM", JournalEntryType.DuringTrade, "T3", "C",
            emotionalState: "FOMO", confidenceLevel: 4);

        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { entry1, entry2, entry3 });

        var result = await _handler.Handle(query, CancellationToken.None);

        result.EmotionSummary.Should().NotBeNull();
        result.EmotionSummary!.TotalEntries.Should().Be(3);
        result.EmotionSummary.Distribution["Tự tin"].Should().Be(2);
        result.EmotionSummary.Distribution["FOMO"].Should().Be(1);
        result.EmotionSummary.AverageConfidence.Should().BeApproximately(6.33, 0.1);
    }

    [Fact]
    public async Task Handle_ToDateMidnight_IncludesEntriesFromThatDay()
    {
        // Arrange — reproduces the bug where frontend sends to="2026-04-11"
        // (parsed as midnight) and journal entries created during that day are excluded
        SetupEmptyDefaults();

        var toDate = new DateTime(2026, 4, 11, 0, 0, 0, DateTimeKind.Utc); // midnight
        var query = new GetSymbolTimelineQuery
        {
            UserId = "user-1",
            Symbol = "BVH",
            From = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc),
            To = toDate
        };

        // Entry created at 10:30 AM on the 'to' date — should NOT be excluded
        var entry = new JournalEntry("user-1", "BVH", JournalEntryType.Observation,
            "Quan sát BVH", "Content",
            timestamp: new DateTime(2026, 4, 11, 10, 30, 0, DateTimeKind.Utc));

        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync(
            "user-1", "BVH", query.From,
            It.Is<DateTime?>(d => d.HasValue && d.Value > toDate), // adjusted to end-of-day
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { entry });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(1);
        result.Items[0].Type.Should().Be("journal");
        result.To.Should().Be(toDate); // DTO echoes original request value, not adjusted

        // Verify repositories received adjusted end-of-day value
        _journalEntryRepo.Verify(r => r.GetByUserIdAndSymbolAsync(
            "user-1", "BVH", query.From,
            It.Is<DateTime?>(d => d.HasValue && d.Value > toDate),
            It.IsAny<CancellationToken>()), Times.Once);
        _marketEventRepo.Verify(r => r.GetBySymbolAsync(
            "BVH", query.From,
            It.Is<DateTime?>(d => d.HasValue && d.Value > toDate),
            It.IsAny<CancellationToken>()), Times.Once);
        _alertHistoryRepo.Verify(r => r.GetByUserIdAndSymbolAsync(
            "user-1", "BVH", query.From,
            It.Is<DateTime?>(d => d.HasValue && d.Value > toDate),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region P7.6: Emotion Trend Over Time

    [Fact]
    public async Task Handle_JournalEntries_CalculatesEmotionTrends_GroupedByMonth()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var entries = new List<JournalEntry>
        {
            new("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
                emotionalState: "Tự tin", confidenceLevel: 8,
                timestamp: new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.PreTrade, "T2", "C",
                emotionalState: "FOMO", confidenceLevel: 5,
                timestamp: new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.DuringTrade, "T3", "C",
                emotionalState: "Bình tĩnh", confidenceLevel: 7,
                timestamp: new DateTime(2025, 2, 5, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.PostTrade, "T4", "C",
                emotionalState: "Bình tĩnh", confidenceLevel: 9,
                timestamp: new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc)),
        };
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary.Should().NotBeNull();
        result.EmotionSummary!.Trends.Should().NotBeNull();
        result.EmotionSummary.Trends.Should().HaveCount(2);

        var jan = result.EmotionSummary.Trends.First(t => t.Period == "2025-01");
        jan.EntryCount.Should().Be(2);
        jan.DominantEmotion.Should().NotBeNullOrEmpty();
        jan.AverageConfidence.Should().BeApproximately(6.5, 0.1);
        jan.Distribution.Should().ContainKey("Tự tin").And.ContainKey("FOMO");

        var feb = result.EmotionSummary.Trends.First(t => t.Period == "2025-02");
        feb.EntryCount.Should().Be(2);
        feb.DominantEmotion.Should().Be("Bình tĩnh");
        feb.AverageConfidence.Should().BeApproximately(8.0, 0.1);
    }

    [Fact]
    public async Task Handle_JournalEntries_EmotionTrends_DominantEmotionIsHighestCount()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var entries = new List<JournalEntry>
        {
            new("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
                emotionalState: "FOMO", confidenceLevel: 3,
                timestamp: new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.PreTrade, "T2", "C",
                emotionalState: "FOMO", confidenceLevel: 4,
                timestamp: new DateTime(2025, 3, 10, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.DuringTrade, "T3", "C",
                emotionalState: "Tự tin", confidenceLevel: 7,
                timestamp: new DateTime(2025, 3, 20, 0, 0, 0, DateTimeKind.Utc)),
        };
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary!.Trends.Should().HaveCount(1);
        result.EmotionSummary.Trends[0].DominantEmotion.Should().Be("FOMO");
        result.EmotionSummary.Trends[0].Distribution["FOMO"].Should().Be(2);
        result.EmotionSummary.Trends[0].Distribution["Tự tin"].Should().Be(1);
    }

    [Fact]
    public async Task Handle_JournalEntries_EmotionTrends_SkipsConfidenceNull()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var entries = new List<JournalEntry>
        {
            new("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
                emotionalState: "Bình tĩnh", confidenceLevel: 6,
                timestamp: new DateTime(2025, 4, 1, 0, 0, 0, DateTimeKind.Utc)),
            new("user-1", "VNM", JournalEntryType.PreTrade, "T2", "C",
                emotionalState: "Bình tĩnh", confidenceLevel: null,
                timestamp: new DateTime(2025, 4, 15, 0, 0, 0, DateTimeKind.Utc)),
        };
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary!.Trends.Should().HaveCount(1);
        result.EmotionSummary.Trends[0].AverageConfidence.Should().BeApproximately(6.0, 0.1);
    }

    [Fact]
    public async Task Handle_EmptyJournalEntries_EmotionTrends_IsEmpty()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — null emotion summary means no trends
        result.EmotionSummary.Should().BeNull();
    }

    #endregion

    #region P7.1: Emotion ↔ P&L Correlation

    [Fact]
    public async Task Handle_EmotionCorrelation_CalculatesAveragePnlPerEmotion()
    {
        // Arrange
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var portfolio = new Portfolio("user-1", "P1", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { portfolio });

        // Trades: BUY 100@50k → SELL 100@55k (gain +10%), BUY 200@60k → SELL 200@54k (loss -10%)
        var buy1 = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 50_000m, 0, 0,
            new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        var sell1 = new Trade(portfolio.Id, "VNM", TradeType.SELL, 100, 55_000m, 0, 0,
            new DateTime(2025, 1, 20, 0, 0, 0, DateTimeKind.Utc));
        var buy2 = new Trade(portfolio.Id, "VNM", TradeType.BUY, 200, 60_000m, 0, 0,
            new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var sell2 = new Trade(portfolio.Id, "VNM", TradeType.SELL, 200, 54_000m, 0, 0,
            new DateTime(2025, 2, 20, 0, 0, 0, DateTimeKind.Utc));

        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { buy1, sell1, buy2, sell2 });

        // Journal linked to trades via TradeId
        var journal1 = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Calm buy", "C",
            emotionalState: "Bình tĩnh", confidenceLevel: 8, tradeId: buy1.Id,
            timestamp: new DateTime(2025, 1, 4, 0, 0, 0, DateTimeKind.Utc));
        var journal2 = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "FOMO buy", "C",
            emotionalState: "FOMO", confidenceLevel: 5, tradeId: buy2.Id,
            timestamp: new DateTime(2025, 1, 31, 0, 0, 0, DateTimeKind.Utc));

        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { journal1, journal2 });

        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());
        _alertHistoryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary.Should().NotBeNull();
        result.EmotionSummary!.Correlations.Should().NotBeNull();
        result.EmotionSummary.Correlations.Should().HaveCount(2);

        var calm = result.EmotionSummary.Correlations.First(c => c.Emotion == "Bình tĩnh");
        calm.TradeCount.Should().Be(1);
        calm.AveragePnlPercent.Should().BeApproximately(10m, 0.5m); // (55k-50k)/50k = +10%
        calm.WinRate.Should().BeApproximately(100.0, 0.1);

        var fomo = result.EmotionSummary.Correlations.First(c => c.Emotion == "FOMO");
        fomo.TradeCount.Should().Be(1);
        fomo.AveragePnlPercent.Should().BeApproximately(-10m, 0.5m); // (54k-60k)/60k = -10%
        fomo.WinRate.Should().BeApproximately(0.0, 0.1);
    }

    [Fact]
    public async Task Handle_EmotionCorrelation_EmptyWhenNoJournalTradeLinks()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        // Journal without TradeId links
        var entry = new JournalEntry("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
            emotionalState: "Bình tĩnh", confidenceLevel: 7);
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { entry });

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary.Should().NotBeNull();
        result.EmotionSummary!.Correlations.Should().NotBeNull();
        result.EmotionSummary.Correlations.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_EmotionCorrelation_OnlyCountsClosedPositions()
    {
        // Arrange — BUY without matching SELL should not appear in correlation
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var portfolio = new Portfolio("user-1", "P1", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { portfolio });

        var buy = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 50_000m, 0, 0,
            new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc));
        // No SELL trade — open position

        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { buy });

        var journal = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Calm buy", "C",
            emotionalState: "Bình tĩnh", confidenceLevel: 8, tradeId: buy.Id);
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { journal });

        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());
        _alertHistoryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert — no correlation because no closed positions
        result.EmotionSummary!.Correlations.Should().BeEmpty();
    }

    #endregion

    #region P7.2: Confidence Calibration

    [Fact]
    public async Task Handle_ConfidenceCalibration_GroupsByRange()
    {
        // Arrange
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var portfolio = new Portfolio("user-1", "P1", 100_000_000m);
        _portfolioRepo.Setup(r => r.GetByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Portfolio> { portfolio });

        // High confidence (9) trade → loss → overconfident
        var buy1 = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 60_000m, 0, 0,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var sell1 = new Trade(portfolio.Id, "VNM", TradeType.SELL, 100, 54_000m, 0, 0,
            new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        // Low confidence (3) trade → win → underconfident
        var buy2 = new Trade(portfolio.Id, "VNM", TradeType.BUY, 100, 50_000m, 0, 0,
            new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        var sell2 = new Trade(portfolio.Id, "VNM", TradeType.SELL, 100, 60_000m, 0, 0,
            new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc));

        _tradeRepo.Setup(r => r.GetByUserPortfoliosAndSymbolAsync(It.IsAny<IEnumerable<string>>(), "VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Trade> { buy1, sell1, buy2, sell2 });

        var journal1 = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "High conf", "C",
            emotionalState: "Tự tin", confidenceLevel: 9, tradeId: buy1.Id,
            timestamp: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        var journal2 = new JournalEntry("user-1", "VNM", JournalEntryType.PreTrade, "Low conf", "C",
            emotionalState: "Lo lắng", confidenceLevel: 3, tradeId: buy2.Id,
            timestamp: new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc));

        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JournalEntry> { journal1, journal2 });

        _marketEventRepo.Setup(r => r.GetBySymbolAsync("VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MarketEvent>());
        _alertHistoryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AlertHistory>());

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary.Should().NotBeNull();
        result.EmotionSummary!.ConfidenceCalibration.Should().NotBeNull();
        result.EmotionSummary.ConfidenceCalibration.Should().HaveCount(2);

        var veryHigh = result.EmotionSummary.ConfidenceCalibration.First(c => c.Range == "Very High (9-10)");
        veryHigh.TradeCount.Should().Be(1);
        veryHigh.WinRate.Should().BeApproximately(0.0, 0.1); // lost trade
        veryHigh.IsCalibrated.Should().BeFalse(); // overconfident

        var low = result.EmotionSummary.ConfidenceCalibration.First(c => c.Range == "Low (1-3)");
        low.TradeCount.Should().Be(1);
        low.WinRate.Should().BeApproximately(100.0, 0.1); // won trade
    }

    [Fact]
    public async Task Handle_ConfidenceCalibration_EmptyWhenNoLinkedTrades()
    {
        // Arrange
        SetupEmptyDefaults();
        var query = new GetSymbolTimelineQuery { UserId = "user-1", Symbol = "VNM" };

        var entries = new List<JournalEntry>
        {
            new("user-1", "VNM", JournalEntryType.Observation, "T1", "C",
                emotionalState: "Bình tĩnh", confidenceLevel: 7)
        };
        _journalEntryRepo.Setup(r => r.GetByUserIdAndSymbolAsync("user-1", "VNM", null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entries);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.EmotionSummary!.ConfidenceCalibration.Should().BeEmpty();
    }

    #endregion
}

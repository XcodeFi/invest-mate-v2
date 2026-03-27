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
}

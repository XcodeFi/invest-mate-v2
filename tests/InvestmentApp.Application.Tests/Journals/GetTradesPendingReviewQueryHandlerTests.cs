using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.Journals.Queries.GetTradesPendingReview;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.Journals;

public class GetTradesPendingReviewQueryHandlerTests
{
    private readonly Mock<ITradeRepository> _tradeRepo;
    private readonly Mock<IPortfolioRepository> _portfolioRepo;
    private readonly Mock<IJournalEntryRepository> _journalEntryRepo;
    private readonly GetTradesPendingReviewQueryHandler _handler;

    public GetTradesPendingReviewQueryHandlerTests()
    {
        _tradeRepo = new Mock<ITradeRepository>();
        _portfolioRepo = new Mock<IPortfolioRepository>();
        _journalEntryRepo = new Mock<IJournalEntryRepository>();
        _handler = new GetTradesPendingReviewQueryHandler(
            _tradeRepo.Object,
            _portfolioRepo.Object,
            _journalEntryRepo.Object);
    }

    [Fact]
    public async Task Handle_SellTradesWithoutPostTradeJournal_ReturnsThoseTrades()
    {
        // Arrange
        var userId = "user-1";
        var portfolioId = "port-1";
        var portfolio = CreatePortfolio(portfolioId, userId, "Test Portfolio");
        var sellTrade = CreateTrade("trade-1", portfolioId, "VNM", TradeType.SELL);

        _portfolioRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sellTrade });
        _journalEntryRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JournalEntry>());

        var query = new GetTradesPendingReviewQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().TradeId.Should().Be("trade-1");
        result.First().Symbol.Should().Be("VNM");
        result.First().PortfolioName.Should().Be("Test Portfolio");
    }

    [Fact]
    public async Task Handle_AllTradesHavePostTradeJournal_ReturnsEmpty()
    {
        // Arrange
        var userId = "user-1";
        var portfolioId = "port-1";
        var portfolio = CreatePortfolio(portfolioId, userId, "Port");
        var sellTrade = CreateTrade("trade-1", portfolioId, "VNM", TradeType.SELL);
        var journal = CreateJournalEntry(userId, "VNM", JournalEntryType.PostTrade, "trade-1");

        _portfolioRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync(portfolioId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sellTrade });
        _journalEntryRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { journal });

        var query = new GetTradesPendingReviewQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FiltersByPortfolioId()
    {
        // Arrange
        var userId = "user-1";
        var port1 = CreatePortfolio("port-1", userId, "Port A");
        var port2 = CreatePortfolio("port-2", userId, "Port B");
        var trade1 = CreateTrade("trade-1", "port-1", "VNM", TradeType.SELL);
        var trade2 = CreateTrade("trade-2", "port-2", "FPT", TradeType.SELL);

        _portfolioRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { port1, port2 });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { trade1 });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { trade2 });
        _journalEntryRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JournalEntry>());

        var query = new GetTradesPendingReviewQuery { UserId = userId, PortfolioId = "port-1" };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Symbol.Should().Be("VNM");
    }

    [Fact]
    public async Task Handle_TradesWithOtherJournalTypes_StillPending()
    {
        // Arrange — trade has Observation journal but NOT PostTrade
        var userId = "user-1";
        var portfolio = CreatePortfolio("port-1", userId, "Port");
        var sellTrade = CreateTrade("trade-1", "port-1", "VNM", TradeType.SELL);
        var observationJournal = CreateJournalEntry(userId, "VNM", JournalEntryType.Observation, "trade-1");

        _portfolioRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { sellTrade });
        _journalEntryRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { observationJournal });

        var query = new GetTradesPendingReviewQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_OnlySellTrades_BuyTradesExcluded()
    {
        // Arrange
        var userId = "user-1";
        var portfolio = CreatePortfolio("port-1", userId, "Port");
        var buyTrade = CreateTrade("trade-buy", "port-1", "VNM", TradeType.BUY);
        var sellTrade = CreateTrade("trade-sell", "port-1", "FPT", TradeType.SELL);

        _portfolioRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { portfolio });
        _tradeRepo.Setup(r => r.GetByPortfolioIdAsync("port-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { buyTrade, sellTrade });
        _journalEntryRepo.Setup(r => r.GetByUserIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<JournalEntry>());

        var query = new GetTradesPendingReviewQuery { UserId = userId };

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.First().Symbol.Should().Be("FPT");
    }

    // --- Helpers ---

    private static Portfolio CreatePortfolio(string id, string userId, string name)
    {
        var p = new Portfolio(userId, name, 100_000_000m);
        typeof(Portfolio).GetProperty("Id")!.SetValue(p, id);
        return p;
    }

    private static Trade CreateTrade(string id, string portfolioId, string symbol, TradeType type)
    {
        var t = new Trade(portfolioId, symbol, type, 100, 50_000m, 0, 0, DateTime.UtcNow.AddDays(-1));
        typeof(Trade).GetProperty("Id")!.SetValue(t, id);
        return t;
    }

    private static JournalEntry CreateJournalEntry(string userId, string symbol, JournalEntryType entryType, string? tradeId = null)
    {
        return new JournalEntry(
            userId: userId,
            symbol: symbol,
            entryType: entryType,
            title: "Review",
            content: "Content",
            tradeId: tradeId);
    }
}

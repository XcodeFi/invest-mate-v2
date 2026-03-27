using FluentAssertions;
using InvestmentApp.Application.Interfaces;
using InvestmentApp.Application.JournalEntries.Commands.CreateJournalEntry;
using InvestmentApp.Domain.Entities;
using Moq;

namespace InvestmentApp.Application.Tests.JournalEntries.Commands;

public class CreateJournalEntryCommandHandlerTests
{
    private readonly Mock<IJournalEntryRepository> _journalEntryRepo;
    private readonly Mock<IMarketDataProvider> _marketDataProvider;
    private readonly Mock<IAuditService> _auditService;
    private readonly CreateJournalEntryCommandHandler _handler;

    public CreateJournalEntryCommandHandlerTests()
    {
        _journalEntryRepo = new Mock<IJournalEntryRepository>();
        _marketDataProvider = new Mock<IMarketDataProvider>();
        _auditService = new Mock<IAuditService>();
        _handler = new CreateJournalEntryCommandHandler(
            _journalEntryRepo.Object,
            _marketDataProvider.Object,
            _auditService.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesEntryAndReturnsId()
    {
        // Arrange
        var command = new CreateJournalEntryCommand
        {
            UserId = "user-1",
            Symbol = "VNM",
            EntryType = "Observation",
            Title = "RSI oversold",
            Content = "RSI giảm xuống 28",
            EmotionalState = "Lo lắng",
            ConfidenceLevel = 5
        };

        _marketDataProvider
            .Setup(m => m.GetCurrentPriceAsync("VNM", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StockPriceData { Symbol = "VNM", Close = 72_000m });
        _marketDataProvider
            .Setup(m => m.GetIndexDataAsync("VNINDEX", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketIndexData { Close = 1_250m });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNullOrEmpty();
        _journalEntryRepo.Verify(r => r.AddAsync(
            It.Is<JournalEntry>(e =>
                e.UserId == "user-1" &&
                e.Symbol == "VNM" &&
                e.EntryType == JournalEntryType.Observation &&
                e.Title == "RSI oversold" &&
                e.PriceAtTime == 72_000m &&
                e.VnIndexAtTime == 1_250m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExplicitPrice_ShouldNotFetchFromProvider()
    {
        var command = new CreateJournalEntryCommand
        {
            UserId = "user-1",
            Symbol = "VNM",
            EntryType = "PreTrade",
            Title = "Phân tích",
            Content = "Content",
            PriceAtTime = 75_000m
        };

        _marketDataProvider
            .Setup(m => m.GetIndexDataAsync("VNINDEX", It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketIndexData?)null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNullOrEmpty();
        _marketDataProvider.Verify(
            m => m.GetCurrentPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _journalEntryRepo.Verify(r => r.AddAsync(
            It.Is<JournalEntry>(e => e.PriceAtTime == 75_000m),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptySymbol_ShouldThrowArgumentException()
    {
        var command = new CreateJournalEntryCommand
        {
            UserId = "user-1",
            Symbol = "",
            EntryType = "Observation",
            Title = "T",
            Content = "C"
        };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MarketDataFails_ShouldStillCreateEntry()
    {
        var command = new CreateJournalEntryCommand
        {
            UserId = "user-1",
            Symbol = "VNM",
            EntryType = "Observation",
            Title = "T",
            Content = "C"
        };

        _marketDataProvider
            .Setup(m => m.GetCurrentPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));
        _marketDataProvider
            .Setup(m => m.GetIndexDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNullOrEmpty();
        _journalEntryRepo.Verify(r => r.AddAsync(
            It.Is<JournalEntry>(e => e.PriceAtTime == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ValidCommand_LogsAudit()
    {
        var command = new CreateJournalEntryCommand
        {
            UserId = "user-1",
            Symbol = "VNM",
            EntryType = "Review",
            Title = "Tổng kết tháng",
            Content = "Content"
        };

        _marketDataProvider
            .Setup(m => m.GetCurrentPriceAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StockPriceData?)null);
        _marketDataProvider
            .Setup(m => m.GetIndexDataAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MarketIndexData?)null);

        await _handler.Handle(command, CancellationToken.None);

        _auditService.Verify(a => a.LogAsync(
            It.Is<AuditEntry>(e =>
                e.Action == "CreatedJournalEntry" &&
                e.EntityType == "JournalEntry"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
